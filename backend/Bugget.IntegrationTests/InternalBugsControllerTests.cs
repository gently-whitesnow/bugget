using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bugget.BO.DomainEvents;
using Bugget.Entities.BO.Common;
using Bugget.Entities.DTO.Internal;
using Bugget.IntegrationTests.Fixtures;
using Dapper;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class InternalBugsControllerTests : IClassFixture<AppWithPostgresFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _client;
    private readonly string _connectionString;

    public InternalBugsControllerTests(AppWithPostgresFixture fixture)
    {
        _client = fixture.CreateClient();
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
    }

    [Fact(DisplayName = "POST /v2/_internal/bugs: создаёт Report+Bug с creator_type=TgBetaTester")]
    public async Task Create_NewReport_PersistsCreatorType()
    {
        var key = Guid.NewGuid().ToString();
        var workspaceId = $"ws_{Guid.NewGuid():N}";
        var creatorUserId = $"tester_{Guid.NewGuid():N}";
        var req = BuildRequest(request =>
        {
            request.Headers.Add("X-Client-Name", "beta-bot");
            request.Headers.Add("Idempotency-Key", key);
        },
        new InternalCreateBugRequestDto
        {
            WorkspaceId = workspaceId,
            CreatorUserId = creatorUserId,
            Receive = "Что-то пошло не так",
            Expect = "Ожидалось корректное поведение",
        });

        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InternalCreateBugResponseDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.True(body!.ReportId > 0);
        Assert.True(body.BugId > 0);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var row = await conn.QuerySingleAsync<(short report_creator_type, short bug_creator_type, string creator_user_id)>(
            @"SELECT r.creator_type AS report_creator_type, b.creator_type AS bug_creator_type, b.creator_user_id
              FROM public.reports r JOIN public.bugs b ON b.report_id = r.id
              WHERE r.id = @rid AND b.id = @bid",
            new { rid = body.ReportId, bid = body.BugId });
        Assert.Equal((short)CreatorType.TgBetaTester, row.report_creator_type);
        Assert.Equal((short)CreatorType.TgBetaTester, row.bug_creator_type);
        Assert.Equal(creatorUserId, row.creator_user_id);

        var sideEffects = await conn.QuerySingleAsync<(long cache_entries, long bug_created_events)>(
            """
            SELECT
                (SELECT count(*) FROM public.idempotency_cache WHERE key = @key) AS cache_entries,
                (SELECT count(*) FROM public.domain_events
                 WHERE workspace_id = @workspaceId
                   AND aggregate_type = @aggregateType
                   AND aggregate_id = @aggregateId
                   AND event_type = @eventType) AS bug_created_events
            """,
            new
            {
                key,
                workspaceId,
                aggregateType = BuggetAggregateTypes.Bug,
                aggregateId = body.BugId.ToString(),
                eventType = BuggetEventTypes.BugCreated,
            });
        Assert.Equal(1, sideEffects.cache_entries);
        Assert.Equal(1, sideEffects.bug_created_events);
    }

    [Fact(DisplayName = "Retry с тем же Idempotency-Key возвращает исходные {reportId, bugId}")]
    public async Task Retry_WithSameKey_ReturnsSameIds()
    {
        var key = Guid.NewGuid().ToString();
        var workspaceId = $"ws_{Guid.NewGuid():N}";
        var creatorUserId = $"tester_{Guid.NewGuid():N}";
        var payload = new InternalCreateBugRequestDto
        {
            WorkspaceId = workspaceId,
            CreatorUserId = creatorUserId,
            Receive = "Повторная отправка после ретрая",
            Expect = "Должен вернуться тот же самый id",
        };

        var first = await _client.SendAsync(BuildRequest(r =>
        {
            r.Headers.Add("X-Client-Name", "beta-bot");
            r.Headers.Add("Idempotency-Key", key);
        }, payload));
        var firstBody = await first.Content.ReadFromJsonAsync<InternalCreateBugResponseDto>(JsonOptions);

        var second = await _client.SendAsync(BuildRequest(r =>
        {
            r.Headers.Add("X-Client-Name", "beta-bot");
            r.Headers.Add("Idempotency-Key", key);
        }, payload));
        var secondBody = await second.Content.ReadFromJsonAsync<InternalCreateBugResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.Equal(firstBody!.ReportId, secondBody!.ReportId);
        Assert.Equal(firstBody.BugId, secondBody.BugId);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var bugCount = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM public.bugs WHERE creator_user_id = @u",
            new { u = creatorUserId });
        Assert.Equal(1, bugCount);
    }

    [Fact(DisplayName = "Concurrent retry с тем же Idempotency-Key создаёт ровно один Report+Bug")]
    public async Task Concurrent_Retry_WithSameKey_Creates_Single_Report_And_Bug()
    {
        var key = Guid.NewGuid().ToString();
        var workspaceId = $"ws_{Guid.NewGuid():N}";
        var creatorUserId = $"tester_{Guid.NewGuid():N}";
        var payload = new InternalCreateBugRequestDto
        {
            WorkspaceId = workspaceId,
            CreatorUserId = creatorUserId,
            Receive = "Конкурентный ретрай должен дедуплицироваться",
            Expect = "Все запросы должны вернуть одну и ту же пару id",
        };

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 6).Select(async _ =>
        {
            await ready.Task;
            var response = await _client.SendAsync(BuildRequest(r =>
            {
                r.Headers.Add("X-Client-Name", "beta-bot");
                r.Headers.Add("Idempotency-Key", key);
            }, payload));
            var body = await response.Content.ReadFromJsonAsync<InternalCreateBugResponseDto>(JsonOptions);
            return (response.StatusCode, body);
        }).ToArray();

        ready.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.All(results, r => Assert.NotNull(r.body));

        var reportIds = results.Select(r => r.body!.ReportId).Distinct().ToArray();
        var bugIds = results.Select(r => r.body!.BugId).Distinct().ToArray();

        Assert.Single(reportIds);
        Assert.Single(bugIds);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var counts = await conn.QuerySingleAsync<(long reports, long bugs)>(
            """
            SELECT
                (SELECT count(*) FROM public.reports WHERE creator_user_id = @u) AS reports,
                (SELECT count(*) FROM public.bugs WHERE creator_user_id = @u) AS bugs
            """,
            new { u = creatorUserId });

        Assert.Equal(1, counts.reports);
        Assert.Equal(1, counts.bugs);
    }

    [Fact(DisplayName = "Отсутствует Idempotency-Key → 400")]
    public async Task Missing_IdempotencyKey_ReturnsBadRequest()
    {
        var payload = new InternalCreateBugRequestDto
        {
            WorkspaceId = $"ws_{Guid.NewGuid():N}",
            CreatorUserId = $"tester_{Guid.NewGuid():N}",
            Receive = "Сообщение достаточной длины",
            Expect = "Ожидание достаточной длины",
        };

        var response = await _client.SendAsync(BuildRequest(r =>
        {
            r.Headers.Add("X-Client-Name", "beta-bot");
        }, payload));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "GET /v2/_internal/bugs/{id}: возвращает карточку с полями репорта и attachments_count")]
    public async Task GetById_Returns_Card_With_Counts()
    {
        var key = Guid.NewGuid().ToString();
        var workspaceId = $"ws_{Guid.NewGuid():N}";
        var creatorUserId = $"tester_{Guid.NewGuid():N}";
        var createReq = BuildRequest(r =>
        {
            r.Headers.Add("X-Client-Name", "beta-bot");
            r.Headers.Add("Idempotency-Key", key);
        }, new InternalCreateBugRequestDto
        {
            WorkspaceId = workspaceId,
            CreatorUserId = creatorUserId,
            Title = "Краш при логине",
            Receive = "Падает на старте",
            Expect = "Должно открываться",
        });
        var created = await (await _client.SendAsync(createReq))
            .Content.ReadFromJsonAsync<InternalCreateBugResponseDto>(JsonOptions);
        Assert.NotNull(created);

        var get = new HttpRequestMessage(HttpMethod.Get, $"/v2/_internal/bugs/{created!.BugId}");
        get.Headers.Add("X-Client-Name", "beta-bot");
        var resp = await _client.SendAsync(get);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<InternalBugDetailResponseDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(created.BugId, body!.BugId);
        Assert.Equal(created.ReportId, body.ReportId);
        Assert.Equal("Краш при логине", body.Title);
        Assert.Equal((int)CreatorType.TgBetaTester, body.CreatorType);
        Assert.Equal(creatorUserId, body.CreatorUserId);
        Assert.NotNull(body.Receive);
        Assert.Equal("Падает на старте", body.Receive);
        Assert.Equal("Должно открываться", body.Expect);
        Assert.Equal(0, body.AttachmentsCount);
    }

    [Fact(DisplayName = "GET /v2/_internal/bugs/{id}: несуществующий bug → 404")]
    public async Task GetById_Unknown_ReturnsNotFound()
    {
        var get = new HttpRequestMessage(HttpMethod.Get, "/v2/_internal/bugs/99999999");
        get.Headers.Add("X-Client-Name", "beta-bot");
        var resp = await _client.SendAsync(get);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact(DisplayName = "GET /v2/_internal/bugs/{id} без X-Client-Name → 401")]
    public async Task GetById_MissingClientName_ReturnsUnauthorized()
    {
        var get = new HttpRequestMessage(HttpMethod.Get, "/v2/_internal/bugs/1");
        var resp = await _client.SendAsync(get);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact(DisplayName = "Отсутствует X-Client-Name → 401")]
    public async Task Missing_ClientName_ReturnsUnauthorized()
    {
        var payload = new InternalCreateBugRequestDto
        {
            WorkspaceId = $"ws_{Guid.NewGuid():N}",
            CreatorUserId = $"tester_{Guid.NewGuid():N}",
            Receive = "Сообщение достаточной длины",
            Expect = "Ожидание достаточной длины",
        };

        var response = await _client.SendAsync(BuildRequest(r =>
        {
            r.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }, payload));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpRequestMessage BuildRequest(
        Action<HttpRequestMessage> configureHeaders,
        InternalCreateBugRequestDto payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v2/_internal/bugs")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        configureHeaders(request);
        return request;
    }
}
