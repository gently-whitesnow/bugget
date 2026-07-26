using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bugget.Entities.BO.Common;
using Bugget.Entities.DTO.Internal;
using Bugget.IntegrationTests.Fixtures;
using Dapper;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class InternalReportsControllerTests : IClassFixture<AppWithPostgresFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _client;
    private readonly string _connectionString;

    public InternalReportsControllerTests(AppWithPostgresFixture fixture)
    {
        _client = fixture.CreateClient();
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
    }

    [Fact(DisplayName = "GET /v2/_internal/reports: только репорты указанного тестера")]
    public async Task List_ReturnsOnlyTesterReports()
    {
        var workspaceId = $"ws_{Guid.NewGuid():N}";
        var testerA = $"tester_a_{Guid.NewGuid():N}";
        var testerB = $"tester_b_{Guid.NewGuid():N}";

        var (reportA, bugA) = await CreateTesterReportAsync(workspaceId, testerA);
        await CreateTesterReportAsync(workspaceId, testerB); // другой тестер

        var body = await ListAsync(workspaceId, testerA, clientName: "beta-bot");

        Assert.NotNull(body);
        var item = Assert.Single(body!.Items);
        Assert.Equal(reportA, item.ReportId);
        Assert.Equal(bugA, item.Bug.BugId);
        Assert.Equal("Что-то пошло не так при запуске", item.ReportTitle);
    }

    [Fact(DisplayName = "I-11: team-added Bug в репорте тестера не попадает в ответ")]
    public async Task List_TeamAddedBug_Hidden()
    {
        var workspaceId = $"ws_{Guid.NewGuid():N}";
        var tester = $"tester_{Guid.NewGuid():N}";

        var (reportId, testerBugId) = await CreateTesterReportAsync(workspaceId, tester);

        // Команда добавила Bug в тот же Report (creator_type=User)
        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync(@"
                INSERT INTO public.bugs (report_id, receive, expect, creator_user_id, status, creator_type)
                VALUES (@reportId, 'team receive', 'team expect', 'team_user', 0, @creatorType);",
                new
                {
                    reportId,
                    creatorType = (short)CreatorType.User,
                });
        }

        var body = await ListAsync(workspaceId, tester, clientName: "beta-bot");
        Assert.NotNull(body);
        var item = Assert.Single(body!.Items);
        Assert.Equal(reportId, item.ReportId);
        Assert.Equal(testerBugId, item.Bug.BugId); // команды не видно
    }

    [Fact(DisplayName = "GET без X-Client-Name → 401")]
    public async Task List_MissingClientName_ReturnsUnauthorized()
    {
        var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"/v2/_internal/reports?workspaceId=ws_x&creatorUserId=tester_x");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact(DisplayName = "GET без workspaceId → 400")]
    public async Task List_MissingWorkspaceId_ReturnsBadRequest()
    {
        var req = new HttpRequestMessage(
            HttpMethod.Get,
            "/v2/_internal/reports?creatorUserId=tester_x");
        req.Headers.Add("X-Client-Name", "beta-bot");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    private async Task<InternalReportsListResponseDto?> ListAsync(
        string workspaceId, string creatorUserId, string? clientName)
    {
        var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"/v2/_internal/reports?workspaceId={workspaceId}&creatorUserId={creatorUserId}");
        if (clientName is not null)
        {
            req.Headers.Add("X-Client-Name", clientName);
        }

        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return await resp.Content.ReadFromJsonAsync<InternalReportsListResponseDto>(JsonOptions);
    }

    private async Task<(int reportId, int bugId)> CreateTesterReportAsync(string workspaceId, string testerId)
    {
        var payload = new InternalCreateBugRequestDto
        {
            WorkspaceId = workspaceId,
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
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<InternalCreateBugResponseDto>(JsonOptions);
        return (body!.ReportId, body.BugId);
    }
}
