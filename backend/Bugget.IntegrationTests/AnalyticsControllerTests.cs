using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bugget.Application.Ports;
using Bugget.Domain.Bugs;
using Bugget.Domain.Reports;
using Bugget.IntegrationTests.Fixtures;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Bugget.IntegrationTests;

/// <summary>
/// Аналитические эндпоинты end-to-end через HTTP pipeline + Postgres testcontainer. Репорты и интервалы сидим
/// напрямую в БД (минуя domain-events poller), чтобы контролировать данные, попадающие в JSON.
/// </summary>
[Collection("PostgresCollection")]
public sealed class AnalyticsControllerTests : IClassFixture<AnalyticsControllerTests.AnalyticsAppFixture>
{
    private const string OrganizationHeader = "X-Organization-Id", TeamHeader = "X-Team-Id", UserHeader = "X-User-Id", TeamId = "test-team";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _client;
    private readonly string _connectionString;
    private readonly AnalyticsSeeder _seeder;
    private readonly string _workspaceId;

    public AnalyticsControllerTests(AnalyticsAppFixture fixture)
    {
        _workspaceId = $"ws_{Guid.NewGuid():N}";
        _client = fixture.CreateClient();
        _client.DefaultRequestHeaders.Add(OrganizationHeader, _workspaceId);
        _client.DefaultRequestHeaders.Add(TeamHeader, TeamId);
        _client.DefaultRequestHeaders.Add(UserHeader, "test-user");
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
        _seeder = new AnalyticsSeeder(_connectionString);
    }

    [Fact(DisplayName = "GET /v2/analytics/summary: closed-in-period репорт попадает, excluded — нет")]
    public async Task Summary_ClosedInPeriod_ExcludesFlagged()
    {
        // 2 репорта в нашем workspace:
        // r1 — closed-in-period (Resolved), 2 Test-интервала (1 регрессия), 1 Fix-интервал.
        // r2 — closed-in-period, но `is_excluded_from_analytics = TRUE` → не в выборке.
        var now = DateTimeOffset.UtcNow;

        var r1 = await _seeder.SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "regression-r1",
            status: ReportStatus.Resolved,
            isExcluded: false,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-5),  now.AddDays(-4),  0),
                new SeedInterval(ReportStatus.Fix,  now.AddDays(-4),  now.AddDays(-3),  0),
                new SeedInterval(ReportStatus.Test, now.AddDays(-3),  now.AddDays(-2),  1),
            ]);

        var r2 = await _seeder.SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "excluded-r2",
            status: ReportStatus.Resolved,
            isExcluded: true,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-5), now.AddDays(-1), 0),
            ]);

        await _seeder.SeedClosedReportAsync(
            workspaceId: $"other_ws_{Guid.NewGuid():N}",
            title: "other-ws-r",
            status: ReportStatus.Resolved,
            isExcluded: false,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-5), now.AddDays(-1), 0),
            ]);

        var resp = await _client.GetAsync("/v2/analytics/summary?period=30d");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("reports_closed").GetInt32());
        Assert.Equal("last_30_days", root.GetProperty("period").GetProperty("label").GetString());

        // У r1 — 2 Test-интервала, значит 1 цикл регрессии → попадает в top.
        var top = root.GetProperty("top_regression_reports");
        Assert.Equal(1, top.GetArrayLength());
        Assert.Equal(r1.ToString(CultureInfo.InvariantCulture), top[0].GetProperty("report_id").GetString());
        Assert.Equal(1, top[0].GetProperty("regression_cycles").GetInt32());
        Assert.Equal("regression-r1", top[0].GetProperty("title").GetString());

        // rework_rate = 1 / 1 = 1.0.
        Assert.Equal(1.0, root.GetProperty("rework_rate").GetDouble());

        // avg_regression_cycles_when_present = 1 (один репорт с регрессией, 1 цикл).
        Assert.Equal(1.0, root.GetProperty("avg_regression_cycles_when_present").GetDouble());

        // phase_time_distribution: 1 Test-initial + 1 Test-retest = 2 дня test;
        // 1 Fix = 1 день fix. test_pct = 2/3 ≈ 0.666, fix_pct = 1/3 ≈ 0.333.
        var dist = root.GetProperty("phase_time_distribution");
        Assert.InRange(dist.GetProperty("test_pct").GetDouble(), 0.6, 0.7);
        Assert.InRange(dist.GetProperty("fix_pct").GetDouble(), 0.3, 0.4);

        Assert.True(r2 > 0);
    }

    [Fact(DisplayName = "GET /v2/reports/{id}/analytics: timeline + bugs_by_status + bugs_added_during_regression")]
    public async Task Report_TimelineAndBugs()
    {
        var now = DateTimeOffset.UtcNow;
        var t1 = now.AddDays(-5);
        var t2 = now.AddDays(-4);
        var t3 = now.AddDays(-3);
        var t4 = now.AddDays(-2);

        var reportId = await _seeder.SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "r-detail",
            status: ReportStatus.Resolved,
            isExcluded: false,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, t1, t2, 0),
                new SeedInterval(ReportStatus.Fix,  t2, t3, 0),
                new SeedInterval(ReportStatus.Test, t3, t4, 1),
            ]);

        // 4 bug'а: 1 Open, 1 Fixed, 1 Verified, 1 Rejected.
        // Один из bug'ов создан во время Test#2 (regression) → bugs_added_during_regression = 1.
        await _seeder.SeedBugAsync(reportId, BugStatus.Open, createdAt: t1.AddHours(2));
        await _seeder.SeedBugAsync(reportId, BugStatus.Fixed, createdAt: t2.AddHours(2));
        await _seeder.SeedBugAsync(reportId, BugStatus.Verified, createdAt: t3.AddMinutes(30));
        await _seeder.SeedBugAsync(reportId, BugStatus.Rejected, createdAt: t4.AddHours(-1));

        var resp = await _client.GetAsync($"/v2/reports/{reportId}/analytics");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(reportId.ToString(CultureInfo.InvariantCulture), root.GetProperty("report_id").GetString());

        Assert.Equal(1, root.GetProperty("regression_cycles").GetInt32());

        var timeline = root.GetProperty("phase_timeline");
        Assert.Equal(3, timeline.GetArrayLength());
        Assert.Equal("Test", timeline[0].GetProperty("phase").GetString());
        Assert.Equal(0, timeline[0].GetProperty("regression_cycle_index").GetInt32());
        Assert.Equal("Fix", timeline[1].GetProperty("phase").GetString());
        Assert.Equal("Test", timeline[2].GetProperty("phase").GetString());
        Assert.Equal(1, timeline[2].GetProperty("regression_cycle_index").GetInt32());

        var bugs = root.GetProperty("bugs_by_status");
        Assert.Equal(1, bugs.GetProperty("open").GetInt32());
        Assert.Equal(1, bugs.GetProperty("fixed").GetInt32());
        Assert.Equal(1, bugs.GetProperty("verified").GetInt32());
        Assert.Equal(1, bugs.GetProperty("rejected").GetInt32());

        Assert.Equal(2, root.GetProperty("bugs_added_during_regression").GetInt32());
    }

    [Fact(DisplayName = "GET /v2/reports/{id}/analytics → 404 если репорт в другом workspace")]
    public async Task Report_NotFound_ForeignWorkspace()
    {
        var foreignWorkspace = $"foreign_ws_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var reportId = await _seeder.SeedClosedReportAsync(
            workspaceId: foreignWorkspace,
            title: "foreign",
            status: ReportStatus.Resolved,
            isExcluded: false,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-3), now.AddDays(-2), 0),
            ]);

        var resp = await _client.GetAsync($"/v2/reports/{reportId}/analytics");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact(DisplayName = "GET /v2/analytics/summary?teamId=...: фильтр creator_team_id отсекает чужие команды")]
    public async Task Team_FiltersByCreatorTeamId()
    {
        var now = DateTimeOffset.UtcNow;
        const long teamId = 42L;
        var teamIdStr = teamId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // r1: наша команда, попадает в выборку.
        var r1 = await _seeder.SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "team-r1",
            status: ReportStatus.Resolved,
            isExcluded: false,
            creatorTeamId: teamIdStr,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-5), now.AddDays(-4), 0),
                new SeedInterval(ReportStatus.Fix,  now.AddDays(-4), now.AddDays(-3), 0),
                new SeedInterval(ReportStatus.Test, now.AddDays(-3), now.AddDays(-2), 1),
            ]);

        // r2: другая команда — НЕ попадает.
        var r2 = await _seeder.SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "other-team-r2",
            status: ReportStatus.Resolved,
            isExcluded: false,
            creatorTeamId: "99",
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-5), now.AddDays(-1), 0),
            ]);

        // r3: без teamId — НЕ попадает (фильтр строгий).
        var r3 = await _seeder.SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "no-team-r3",
            status: ReportStatus.Resolved,
            isExcluded: false,
            creatorTeamId: null,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-5), now.AddDays(-1), 0),
            ]);

        var resp = await _client.GetAsync($"/v2/analytics/summary?period=30d&teamId={teamId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("reports_closed").GetInt32());
        var top = root.GetProperty("top_regression_reports");
        Assert.Equal(1, top.GetArrayLength());
        Assert.Equal(r1.ToString(CultureInfo.InvariantCulture), top[0].GetProperty("report_id").GetString());
        Assert.True(r2 > 0 && r3 > 0);
    }

    [Fact(DisplayName = "GET /v2/analytics/responsible/{userId}: participated требует наличия записи в report_participants")]
    public async Task Responsible_ParticipatedRequiresParticipation()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = $"resp_{Guid.NewGuid():N}";

        // r1: активный (status=Test) репорт с интервалом в окне периода — пользователь участвует.
        var r1 = await _seeder.SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "active-with-participant",
            status: ReportStatus.Test,
            isExcluded: false,
            creatorTeamId: null,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-5), null, 0),
            ]);
        await _seeder.SeedParticipantAsync(r1, userId);

        // r2: активный репорт, но пользователь НЕ участник → не входит.
        var r2 = await _seeder.SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "active-without-participant",
            status: ReportStatus.Fix,
            isExcluded: false,
            creatorTeamId: null,
            intervals:
            [
                new SeedInterval(ReportStatus.Fix, now.AddDays(-2), null, 0),
            ]);

        // r3: completed-репорт пользователя — попадает в reports_completed.
        var r3 = await _seeder.SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "completed-by-user",
            status: ReportStatus.Resolved,
            isExcluded: false,
            creatorTeamId: null,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-5), now.AddDays(-4), 0),
                new SeedInterval(ReportStatus.Fix,  now.AddDays(-4), now.AddDays(-3), 0),
            ]);
        await _seeder.SeedParticipantAsync(r3, userId);

        // r4: excluded — не учитывается.
        var r4 = await _seeder.SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "excluded",
            status: ReportStatus.Resolved,
            isExcluded: true,
            creatorTeamId: null,
            intervals:
            [
                new SeedInterval(ReportStatus.Fix, now.AddDays(-5), now.AddDays(-2), 0),
            ]);
        await _seeder.SeedParticipantAsync(r4, userId);

        var resp = await _client.GetAsync($"/v2/analytics/responsible/{userId}?period=30d");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var participated = root.GetProperty("reports_participated");
        Assert.Equal(1, participated.GetArrayLength());
        Assert.Equal(r1.ToString(CultureInfo.InvariantCulture), participated[0].GetProperty("report_id").GetString());

        var completed = root.GetProperty("reports_completed");
        Assert.Equal(1, completed.GetArrayLength());
        Assert.Equal(r3.ToString(CultureInfo.InvariantCulture), completed[0].GetProperty("report_id").GetString());
        Assert.Equal("Resolved", completed[0].GetProperty("outcome").GetString());

        // avg_fix_phase_days = 1 day (r3.Fix-интервал длиной 1 день).
        Assert.InRange(root.GetProperty("avg_fix_phase_days").GetDouble(), 0.99, 1.01);

        Assert.True(r2 > 0 && r4 > 0);
    }

    [Fact(DisplayName = "PATCH /v2/reports/{id}: is_excluded_from_analytics обновляет БД и пишет в outbox при изменении")]
    public async Task PatchReport_TogglesIsExcluded()
    {
        var workspaceId = _workspaceId;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var reportId = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO public.reports (
                title, status, responsible_user_id, creator_user_id,
                created_at, updated_at, creator_organization_id, is_excluded_from_analytics
            ) VALUES (
                'patch-target', 0, '', 'seed-user',
                now(), now(), @workspaceId, FALSE
            ) RETURNING id;",
            new { workspaceId });

        var resp = await _client.PatchAsJsonAsync(
            $"/v2/reports/{reportId}",
            new { is_excluded_from_analytics = true });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var isExcluded = await conn.ExecuteScalarAsync<bool>(
            "SELECT is_excluded_from_analytics FROM public.reports WHERE id = @reportId;",
            new { reportId });
        Assert.True(isExcluded);

        var eventCount = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)::int FROM public.domain_events
            WHERE aggregate_id = @aggId
              AND event_type = 'bugget.report.excluded_from_analytics_toggled';",
            new { aggId = reportId.ToString() });
        Assert.Equal(1, eventCount);

        // Повторный PATCH с тем же значением — событие не пишется (no-op).
        var resp2 = await _client.PatchAsJsonAsync(
            $"/v2/reports/{reportId}",
            new { is_excluded_from_analytics = true });
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);

        var eventCountAfter = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)::int FROM public.domain_events
            WHERE aggregate_id = @aggId
              AND event_type = 'bugget.report.excluded_from_analytics_toggled';",
            new { aggId = reportId.ToString() });
        Assert.Equal(1, eventCountAfter);

        var resp3 = await _client.PatchAsJsonAsync(
            $"/v2/reports/{reportId}",
            new { is_excluded_from_analytics = false });
        Assert.Equal(HttpStatusCode.OK, resp3.StatusCode);

        var eventCountFinal = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)::int FROM public.domain_events
            WHERE aggregate_id = @aggId
              AND event_type = 'bugget.report.excluded_from_analytics_toggled';",
            new { aggId = reportId.ToString() });
        Assert.Equal(2, eventCountFinal);
    }

    /// <summary>
    /// Явные auth-headers: иначе глобальный <c>[Authorize]</c> и дефолтный <c>default-user</c> с
    /// <c>OrganizationId=null</c> не дадут проверить workspace-фильтрацию.
    /// </summary>
    public sealed class AnalyticsAppFixture(PostgresContainerFixture container) : WebApplicationFactory<Program>
    {
        private readonly PostgreSqlContainer _db = container.Container;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", _db.GetConnectionString());
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "development");

            var fileStorageDir = Path.Combine(Path.GetTempPath(), "bugget-tests-analytics-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fileStorageDir);
            builder.UseSetting("FileStorageOptions:BaseDirectory", fileStorageDir);

            builder.UseSetting("ExternalSettings:Authentication:UserIdHeaderName", UserHeader);
            builder.UseSetting("ExternalSettings:Authentication:OrganizationIdHeaderName", OrganizationHeader).UseSetting("ExternalSettings:Authentication:TeamIdHeaderName", TeamHeader);

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<ITaskQueue>();
                services.AddSingleton<ITaskQueue>(sp => new SyncTaskQueue(sp));
            });
        }
    }
}
