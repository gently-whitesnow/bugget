using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bugget.Application.Commands.Report;
using Bugget.Application.Ports;
using Bugget.Contracts.Reports.Generated;
using Bugget.Domain.Common;
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
        _client.DefaultRequestHeaders.Add("X-Test-Workspace-Id", "test-workspace");
        _client.DefaultRequestHeaders.Add("X-Test-Team-Id", "test-team");
        var scope = fixture.Services.CreateScope();
        _reportsDbClient = scope.ServiceProvider.GetRequiredService<IReportsDbClient>();
    }

    [Fact(DisplayName = "POST /v2/reports/counts:batch — happy path: возвращает counts для каждого scope key")]
    public async Task Batch_HappyPath_ReturnsCountsForEachScope()
    {
        var team = $"team_{Guid.NewGuid():N}";
        var userId = $"user_{Guid.NewGuid():N}";

        var betaReport = await _reportsDbClient.CreateReportAsync(
            userId, team, organizationId: "test-workspace", new ReportCreateDto { Title = "Beta backlog" });
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

        var body = await resp.Content.ReadFromJsonAsync<ReportCountsBatchResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(["beta-active", "team-active"], body!.Counts.Select(item => item.Key));

        var beta = body.Counts.Single(item => item.Key == "beta-active").Count;
        var teamActive = body.Counts.Single(item => item.Key == "team-active").Count;
        Assert.True(beta >= 1, "beta-active должен включать созданный нами beta-tester report");
        Assert.True(teamActive >= beta, "team-active без creator_types должен быть супермножеством beta-active");
    }

    /// <summary>
    /// Ключ среза — данные клиента, а не имя поля: `_` и заглавные в нём обязаны
    /// доехать до ответа дословно. Ровно ради этого счётчики отдаются массивом,
    /// а не картой со свободными ключами (ADR-0009).
    /// </summary>
    [Fact(DisplayName = "POST /v2/reports/counts:batch — ключ с `_` и заглавными возвращается дословно")]
    public async Task Batch_KeyWithUnderscoreAndCaps_IsReturnedVerbatim()
    {
        string[] keys = ["my_scope_key", "MyScopeKey", "Mixed_Case_KEY"];

        var resp = await PostAsync(new { scopes = keys.Select(key => new { key }).ToArray() });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<ReportCountsBatchResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(keys, body!.Counts.Select(item => item.Key));
    }

    [Fact(DisplayName = "POST /v2/reports/counts:batch — empty scopes → 200, пустой counts")]
    public async Task Batch_EmptyScopes_ReturnsEmptyList()
    {
        var resp = await PostAsync(new { scopes = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<ReportCountsBatchResponse>(JsonOptions);
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

    /// <summary>
    /// Ключ ошибки валидации приходит из настоящего model binding, а не из руками собранного
    /// ModelStateDictionary: только так видно, что нормализуется весь body-путь, включая
    /// вложенный сегмент и индекс массива. Клиент отправляет `scopes[0].key` — его и обязан
    /// увидеть в ответе, а не CLR-путь `Scopes[0].Key`.
    /// </summary>
    [Fact(DisplayName = "POST /v2/reports/counts:batch — отсутствует вложенный key → errors по wire-пути scopes[0].key")]
    public async Task Batch_MissingNestedKey_ReportsWireErrorPath()
    {
        var resp = await PostAsync(new { scopes = new object[] { new { statuses = new[] { 0 } } } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("application/problem+json", resp.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal("model_state_validation_error", root.GetProperty("code").GetString());

        var errors = root.GetProperty("errors");
        Assert.True(errors.TryGetProperty("scopes[0].key", out var messages), "ключ ошибки обязан быть wire-путём");
        Assert.NotEmpty(messages.EnumerateArray());
        foreach (var name in errors.EnumerateObject())
        {
            Assert.DoesNotContain("Scopes", name.Name, StringComparison.Ordinal);
        }
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
