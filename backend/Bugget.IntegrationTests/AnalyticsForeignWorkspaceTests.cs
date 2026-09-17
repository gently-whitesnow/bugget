using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bugget.Domain.Reports;
using Dapper;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

/// <summary>
/// Изоляция workspace в analytics-эндпоинтах и гонка PATCH `is_excluded_from_analytics`. Отдельный класс,
/// чтобы не раздувать <see cref="AnalyticsControllerTests"/> сверх ratchet'а maintainability.
/// </summary>
[Collection("PostgresCollection")]
public sealed class AnalyticsForeignWorkspaceTests
    : IClassFixture<AnalyticsControllerTests.AnalyticsAppFixture>
{
    private const string OrganizationHeader = "X-Organization-Id", TeamHeader = "X-Team-Id", UserHeader = "X-User-Id", TeamId = "test-team";

    private readonly HttpClient _client;
    private readonly string _connectionString;
    private readonly AnalyticsSeeder _seeder;
    private readonly string _workspaceId;

    public AnalyticsForeignWorkspaceTests(AnalyticsControllerTests.AnalyticsAppFixture fixture)
    {
        _workspaceId = $"ws_{Guid.NewGuid():N}";
        _client = fixture.CreateClient();
        _client.DefaultRequestHeaders.Add(OrganizationHeader, _workspaceId);
        _client.DefaultRequestHeaders.Add(TeamHeader, TeamId);
        _client.DefaultRequestHeaders.Add(UserHeader, "test-user");
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
        _seeder = new AnalyticsSeeder(_connectionString);
    }

    [Fact(DisplayName = "GET /v2/analytics/summary: репорт из чужого workspace полностью исключён")]
    public async Task Summary_ForeignWorkspaceReport_ExcludedFromOwnSummary()
    {
        // Workspace-фильтр в SQL отсекает чужие репорты и из reports_closed, и из top_regression_reports.
        var foreignWorkspace = $"foreign_ws_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        // Чужой репорт с регрессией (≥2 Test интервала) — кандидат на top.
        var foreignReportId = await _seeder.SeedClosedReportAsync(
            workspaceId: foreignWorkspace,
            title: "foreign-regression",
            status: ReportStatus.Resolved,
            isExcluded: false,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-5), now.AddDays(-4), 0),
                new SeedInterval(ReportStatus.Fix,  now.AddDays(-4), now.AddDays(-3), 0),
                new SeedInterval(ReportStatus.Test, now.AddDays(-3), now.AddDays(-2), 1),
            ]);

        var resp = await _client.GetAsync("/v2/analytics/summary?period=30d");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(0, root.GetProperty("reports_closed").GetInt32());
        Assert.Equal(0, root.GetProperty("top_regression_reports").GetArrayLength());

        Assert.True(foreignReportId > 0);
    }

    [Fact(DisplayName = "GET /v2/analytics/summary?teamId=...: teamId из чужого workspace → пустой результат")]
    public async Task Teams_ForeignWorkspaceTeamId_ReturnsEmpty()
    {
        // Даже если teamId из чужого workspace и там есть репорты этой команды — наш workspace их не видит.
        var foreignWorkspace = $"foreign_ws_{Guid.NewGuid():N}";
        const long foreignTeamId = 7777L;
        var now = DateTimeOffset.UtcNow;

        var foreignReportId = await _seeder.SeedClosedReportAsync(
            workspaceId: foreignWorkspace,
            title: "foreign-team-report",
            status: ReportStatus.Resolved,
            isExcluded: false,
            creatorTeamId: foreignTeamId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-5), now.AddDays(-4), 0),
                new SeedInterval(ReportStatus.Fix,  now.AddDays(-4), now.AddDays(-3), 0),
                new SeedInterval(ReportStatus.Test, now.AddDays(-3), now.AddDays(-2), 1),
            ]);

        var resp = await _client.GetAsync($"/v2/analytics/summary?period=30d&teamId={foreignTeamId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(0, root.GetProperty("reports_closed").GetInt32());
        Assert.Equal(0, root.GetProperty("top_regression_reports").GetArrayLength());

        Assert.True(foreignReportId > 0);
    }

    [Fact(DisplayName = "GET /v2/analytics/responsible/{userId}: userId из чужого workspace → пустые participated/completed")]
    public async Task Responsible_ForeignWorkspaceUserId_ReturnsEmpty()
    {
        // userId, участвующий только в чужих репортах, не должен утечь в /responsible: оба списка пустые.
        var foreignWorkspace = $"foreign_ws_{Guid.NewGuid():N}";
        var foreignUserId = $"foreign_user_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        var foreignActive = await _seeder.SeedClosedReportAsync(
            workspaceId: foreignWorkspace,
            title: "foreign-active",
            status: ReportStatus.Test,
            isExcluded: false,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-3), null, 0),
            ]);
        await _seeder.SeedParticipantAsync(foreignActive, foreignUserId);

        var foreignCompleted = await _seeder.SeedClosedReportAsync(
            workspaceId: foreignWorkspace,
            title: "foreign-completed",
            status: ReportStatus.Resolved,
            isExcluded: false,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-5), now.AddDays(-4), 0),
                new SeedInterval(ReportStatus.Fix,  now.AddDays(-4), now.AddDays(-3), 0),
            ]);
        await _seeder.SeedParticipantAsync(foreignCompleted, foreignUserId);

        var resp = await _client.GetAsync($"/v2/analytics/responsible/{foreignUserId}?period=30d");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(0, root.GetProperty("reports_participated").GetArrayLength());
        Assert.Equal(0, root.GetProperty("reports_completed").GetArrayLength());

        Assert.True(foreignActive > 0 && foreignCompleted > 0);
    }

    [Fact(DisplayName = "PATCH /v2/reports/{id}: concurrent toggle на одну строку → ровно одно событие (FOR UPDATE row lock)")]
    public async Task PatchReport_ConcurrentToggle_OnlyOneEventEmitted()
    {
        // Без FOR UPDATE два параллельных PATCH (false→true) читали одно `false` и оба эмитили событие;
        // с row lock второй ждёт первый, читает `true` и фиксирует no-op.
        var workspaceId = _workspaceId;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var reportId = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO public.reports (
                title, status, responsible_user_id, creator_user_id,
                created_at, updated_at, creator_organization_id, is_excluded_from_analytics
            ) VALUES (
                'race-target', 0, '', 'seed-user',
                now(), now(), @workspaceId, FALSE
            ) RETURNING id;",
            new { workspaceId });

        const int parallelism = 5;
        var tasks = new Task<HttpResponseMessage>[parallelism];
        for (var i = 0; i < parallelism; i++)
        {
            tasks[i] = _client.PatchAsJsonAsync(
                $"/v2/reports/{reportId}",
                new { is_excluded_from_analytics = true });
        }
        var responses = await Task.WhenAll(tasks);

        foreach (var r in responses)
        {
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        var finalFlag = await conn.ExecuteScalarAsync<bool>(
            "SELECT is_excluded_from_analytics FROM public.reports WHERE id = @reportId;",
            new { reportId });
        Assert.True(finalFlag);

        var eventCount = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)::int FROM public.domain_events
            WHERE aggregate_id = @aggId
              AND event_type = 'bugget.report.excluded_from_analytics_toggled';",
            new { aggId = reportId.ToString() });
        Assert.Equal(1, eventCount);
    }
}
