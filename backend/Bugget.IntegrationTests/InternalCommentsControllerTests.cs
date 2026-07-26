using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bugget.Entities.BO.Common;
using Bugget.Entities.DTO.Internal;
using Bugget.IntegrationTests.Fixtures;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class InternalCommentsControllerTests : IClassFixture<AppWithPostgresFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _client;
    private readonly string _connectionString;
    private readonly FakeReportPageHubClient _hub;
    private readonly PostgresContainerFixture _postgres;

    public InternalCommentsControllerTests(AppWithPostgresFixture fixture, PostgresContainerFixture postgres)
    {
        _client = fixture.CreateClient();
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
        _hub = fixture.Services.GetRequiredService<FakeReportPageHubClient>();
        _postgres = postgres;
    }

    [Fact(DisplayName = "POST .../comments: создаёт комментарий с force audience=External")]
    public async Task Create_ForcesAudienceExternal()
    {
        var (_, bugId, testerId) = await CreateBugAsync();

        var resp = await _client.SendAsync(BuildPost(bugId, new InternalCreateCommentRequestDto
        {
            CreatorType = (int)CreatorType.TgBetaTester,
            CreatorUserId = testerId,
            Text = "Да, воспроизводится",
        }, clientName: "beta-bot"));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<InternalCreateCommentResponseDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.True(body!.CommentId > 0);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var row = await conn.QuerySingleAsync<(short audience, short creator_type, string creator_user_id)>(
            "SELECT audience, creator_type, creator_user_id FROM public.comments WHERE id = @id",
            new { id = body.CommentId });
        Assert.Equal((short)CommentAudience.External, row.audience);
        Assert.Equal((short)CreatorType.TgBetaTester, row.creator_type);
        Assert.Equal(testerId, row.creator_user_id);
    }

    [Fact(DisplayName = "POST .../comments: пушит ReceiveCommentCreate в SignalR-хаб (audience=External)")]
    public async Task Create_PushesSignalRCommentCreate()
    {
        var (reportId, bugId, testerId) = await CreateBugAsync();

        var resp = await _client.SendAsync(BuildPost(bugId, new InternalCreateCommentRequestDto
        {
            CreatorType = (int)CreatorType.TgBetaTester,
            CreatorUserId = testerId,
            Text = "ответ тестера",
        }, clientName: "beta-bot"));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<InternalCreateCommentResponseDto>(JsonOptions);

        var pushed = _hub.CommentCreates.SingleOrDefault(x => x.Comment.Id == body!.CommentId);
        Assert.NotEqual(default, pushed);
        // Default AliasMode → aliasId = raw reportId.ToString().
        Assert.EndsWith(reportId.ToString(), pushed.GroupKey);
        Assert.Equal((int)CommentAudience.External, pushed.Comment.Audience);
        Assert.Equal((int)CreatorType.TgBetaTester, pushed.Comment.CreatorType);
    }

    [Fact(DisplayName = "POST .../comments: SignalR groupKey содержит public_id при AliasMode=guid (SaaS)")]
    public async Task Create_PushesSignalRCommentCreate_GuidAliasMode()
    {
        // SaaS-конфиг: server-wide AliasMode=guid. Бот должен резолвить aliasId через
        // ReportIdResolveHelper.ToAliasId, иначе клиенты, join'ящиеся по public_id-Guid,
        // не получают realtime push.
        await using var guidFactory = new AppWithPostgresFixture(_postgres);
        guidFactory.AliasModeOverride = "guid";
        var client = guidFactory.CreateClient();
        var hub = guidFactory.Services.GetRequiredService<FakeReportPageHubClient>();

        var (reportId, bugId, testerId) = await CreateBugAsync(client);

        var resp = await client.SendAsync(BuildPost(bugId, new InternalCreateCommentRequestDto
        {
            CreatorType = (int)CreatorType.TgBetaTester,
            CreatorUserId = testerId,
            Text = "ответ тестера (guid mode)",
        }, clientName: "beta-bot"));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<InternalCreateCommentResponseDto>(JsonOptions);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var publicId = await conn.QuerySingleAsync<Guid>(
            "SELECT public_id FROM public.reports WHERE id = @id;",
            new { id = reportId });

        var pushed = hub.CommentCreates.SingleOrDefault(x => x.Comment.Id == body!.CommentId);
        Assert.NotEqual(default, pushed);
        Assert.EndsWith(publicId.ToString(), pushed.GroupKey);
    }

    [Fact(DisplayName = "GET .../external-comments: mixed External+Internal → только External")]
    public async Task List_ReturnsOnlyExternal()
    {
        var (_, bugId, testerId) = await CreateBugAsync();

        // 1 external через _internal POST
        var ext = await _client.SendAsync(BuildPost(bugId, new InternalCreateCommentRequestDto
        {
            CreatorType = (int)CreatorType.TgBetaTester,
            CreatorUserId = testerId,
            Text = "внешний ответ",
        }, clientName: "beta-bot"));
        ext.EnsureSuccessStatusCode();

        // 1 internal — напрямую через SQL, имитирует обычный team comment
        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync(
                "SELECT public.create_comment_internal(@u, @b, @t, @ct, @a);",
                new
                {
                    u = "team_user",
                    b = bugId,
                    t = "internal note",
                    ct = (short)CreatorType.User,
                    a = (short)CommentAudience.Internal,
                });
        }

        var req = new HttpRequestMessage(HttpMethod.Get, $"/v2/_internal/bugs/{bugId}/external-comments?sinceId=0&limit=50");
        req.Headers.Add("X-Client-Name", "beta-bot");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<InternalExternalCommentsResponseDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.Single(body!.Items);
        Assert.Equal("внешний ответ", body.Items[0].Text);
        Assert.Equal((int)CreatorType.TgBetaTester, body.Items[0].CreatorType);

        // I-1: response JSON не содержит ключа `audience`.
        var raw = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("audience", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "GET на несуществующий bug → 404")]
    public async Task List_UnknownBug_ReturnsNotFound()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/v2/_internal/bugs/99999999/external-comments?sinceId=0");
        req.Headers.Add("X-Client-Name", "beta-bot");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact(DisplayName = "POST без X-Client-Name → 401")]
    public async Task Create_MissingClientName_ReturnsUnauthorized()
    {
        var (_, bugId, testerId) = await CreateBugAsync();

        var resp = await _client.SendAsync(BuildPost(bugId, new InternalCreateCommentRequestDto
        {
            CreatorType = (int)CreatorType.TgBetaTester,
            CreatorUserId = testerId,
            Text = "x",
        }, clientName: null));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    private HttpRequestMessage BuildPost(int bugId, InternalCreateCommentRequestDto payload, string? clientName)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/v2/_internal/bugs/{bugId}/comments")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        if (clientName is not null)
        {
            req.Headers.Add("X-Client-Name", clientName);
        }

        return req;
    }

    private Task<(int reportId, int bugId, string testerId)> CreateBugAsync()
        => CreateBugAsync(_client);

    private static async Task<(int reportId, int bugId, string testerId)> CreateBugAsync(HttpClient client)
    {
        var testerId = $"tester_{Guid.NewGuid():N}";
        var payload = new InternalCreateBugRequestDto
        {
            WorkspaceId = $"ws_{Guid.NewGuid():N}",
            CreatorUserId = testerId,
            Receive = "Что-то пошло не так при запуске",
            Expect = "Должно было запуститься штатно",
        };
        var req = new HttpRequestMessage(HttpMethod.Post, "/v2/_internal/bugs")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        req.Headers.Add("X-Client-Name", "beta-bot");
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<InternalCreateBugResponseDto>(JsonOptions);
        return (body!.ReportId, body.BugId, testerId);
    }
}
