using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bugget.DA.Interfaces;
using Bugget.Entities.BO.Common;
using Bugget.Entities.DTO.Report;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class ReportCountsControllerTests : IClassFixture<AppWithPostgresFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _client;
    private readonly IReportsDbClient _reportsDbClient;

    public ReportCountsControllerTests(AppWithPostgresFixture fixture)
    {
        _client = fixture.CreateClient();
        var scope = fixture.Services.CreateScope();
        _reportsDbClient = scope.ServiceProvider.GetRequiredService<IReportsDbClient>();
    }

    [Fact(DisplayName = "POST /v2/reports/counts:batch — happy path: возвращает counts для каждого scope key")]
    public async Task Batch_HappyPath_ReturnsCountsKeyedByScope()
    {
        var team = $"team_{Guid.NewGuid():N}";
        var userId = $"user_{Guid.NewGuid():N}";

        var betaReport = await _reportsDbClient.CreateReportAsync(
            userId, team, organizationId: null, new ReportCreateDto { Title = "Beta backlog" });
        await SetReportCreatorTypeAsync(betaReport.Id, (short)CreatorType.TgBetaTester);

        var request = new
        {
            scopes = new object[]
            {
                new { key = "beta-active", team_id = team, statuses = new[] { 0, 2 }, creator_types = new short[] { (short)CreatorType.TgBetaTester } },
                new { key = "team-active", team_id = team, statuses = new[] { 0, 2 } },
            }
        };

        var resp = await PostAsync(request);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<ReportCountsBatchResponseDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.True(body!.Counts.ContainsKey("beta-active"));
        Assert.True(body.Counts.ContainsKey("team-active"));
        Assert.True(body.Counts["beta-active"] >= 1, "beta-active должен включать созданный нами beta-tester report");
        Assert.True(body.Counts["team-active"] >= body.Counts["beta-active"],
            "team-active без creator_types должен быть супермножеством beta-active");
    }

    [Fact(DisplayName = "POST /v2/reports/counts:batch — empty scopes → 200, пустой counts")]
    public async Task Batch_EmptyScopes_ReturnsEmptyDict()
    {
        var resp = await PostAsync(new { scopes = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<ReportCountsBatchResponseDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body!.Counts);
    }

    [Fact(DisplayName = "POST /v2/reports/counts:batch — >50 scopes → 400")]
    public async Task Batch_TooManyScopes_ReturnsBadRequest()
    {
        var scopes = Enumerable.Range(0, 51)
            .Select(i => new { key = $"k{i}" })
            .ToArray();

        var resp = await PostAsync(new { scopes });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact(DisplayName = "POST /v2/reports/counts:batch — unknown поле в scope → 400")]
    public async Task Batch_UnknownField_ReturnsBadRequest()
    {
        var raw = "{\"scopes\":[{\"key\":\"x\",\"unknown_field\":1}]}";
        using var content = new StringContent(raw, Encoding.UTF8, "application/json");
        var resp = await _client.PostAsync("/v2/reports/counts:batch", content);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact(DisplayName = "POST /v2/reports/counts:batch — duplicate key → 400")]
    public async Task Batch_DuplicateKey_ReturnsBadRequest()
    {
        var request = new
        {
            scopes = new object[]
            {
                new { key = "dup" },
                new { key = "dup" },
            }
        };
        var resp = await PostAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact(DisplayName = "POST /v2/reports/counts:batch — пустой key → 400")]
    public async Task Batch_EmptyKey_ReturnsBadRequest()
    {
        var request = new
        {
            scopes = new object[]
            {
                new { key = "" },
            }
        };
        var resp = await PostAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    private Task<HttpResponseMessage> PostAsync(object payload)
    {
        var content = JsonContent.Create(payload, options: JsonOptions);
        return _client.PostAsync("/v2/reports/counts:batch", content);
    }

    private static async Task SetReportCreatorTypeAsync(int reportId, short creatorType)
    {
        var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
        await using var ds = NpgsqlDataSource.Create(connString);
        await using var conn = await ds.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.reports SET creator_type = @ct WHERE id = @id";
        cmd.Parameters.AddWithValue("@ct", creatorType);
        cmd.Parameters.AddWithValue("@id", reportId);
        await cmd.ExecuteNonQueryAsync();
    }
}
