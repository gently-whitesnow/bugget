using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bugget.Entities.BO.Bugs;
using Bugget.Entities.BO.ReportBo;
using Bugget.IntegrationTests.Fixtures;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using TaskQueue;
using Testcontainers.PostgreSql;
using Xunit;

namespace Bugget.IntegrationTests;

/// <summary>
/// Расширение E2E-покрытия <c>/v2/analytics</c>: двойная регрессия, чистый цикл
/// без регрессии, ISO-week boundary, PATCH-toggle → exclude из summary,
/// bugs_added_during_regression. Вынесено отдельным файлом, чтобы оставаться
/// в пределах maintainability budget (TYPE_LOC ≤ 500).
/// </summary>
[Collection("PostgresCollection")]
public sealed class AnalyticsControllerT16Tests : IClassFixture<AnalyticsControllerT16Tests.AnalyticsT16Fixture>
{
    private const string OrganizationHeader = "X-Organization-Id";
    private const string UserHeader = "X-User-Id";

    private readonly HttpClient _client;
    private readonly string _connectionString;
    private readonly string _workspaceId;

    public AnalyticsControllerT16Tests(AnalyticsT16Fixture fixture)
    {
        _workspaceId = $"ws_{Guid.NewGuid():N}";
        _client = fixture.CreateClient();
        _client.DefaultRequestHeaders.Add(OrganizationHeader, _workspaceId);
        _client.DefaultRequestHeaders.Add(UserHeader, "test-user");
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
    }

    [Fact(DisplayName = "summary: репорт Test→Fix→Test→Fix→Test→Resolved считается двумя циклами регрессии")]
    public async Task Summary_DoubleRegression_RegressionCyclesAndDistribution()
    {
        // r1: 3 Test-интервала (initial + 2 retest) и 2 Fix-интервала → 2 цикла регрессии.
        // Каждый интервал — 1 день: итого 3 дня test, 2 дня fix; test_pct + fix_pct == 1.0 (60/40).
        var now = DateTimeOffset.UtcNow;
        var r1 = await SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "double-regression-r1",
            status: ReportStatus.Resolved,
            isExcluded: false,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-10), now.AddDays(-9), 0),
                new SeedInterval(ReportStatus.Fix,  now.AddDays(-9),  now.AddDays(-8), 0),
                new SeedInterval(ReportStatus.Test, now.AddDays(-8),  now.AddDays(-7), 1),
                new SeedInterval(ReportStatus.Fix,  now.AddDays(-7),  now.AddDays(-6), 1),
                new SeedInterval(ReportStatus.Test, now.AddDays(-6),  now.AddDays(-5), 2),
            ]);

        var resp = await _client.GetAsync("/v2/analytics/summary?period=30d");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("reports_closed").GetInt32());
        Assert.Equal(1.0, root.GetProperty("rework_rate").GetDouble());
        // avg_regression_cycles_when_present = 2 (3 Test-интервала → 2 регрессии).
        Assert.Equal(2.0, root.GetProperty("avg_regression_cycles_when_present").GetDouble());

        var top = root.GetProperty("top_regression_reports");
        Assert.Equal(1, top.GetArrayLength());
        Assert.Equal((long)r1, top[0].GetProperty("report_id").GetInt64());
        Assert.Equal(2, top[0].GetProperty("regression_cycles").GetInt32());

        var dist = root.GetProperty("phase_time_distribution");
        var testPct = dist.GetProperty("test_pct").GetDouble();
        var fixPct = dist.GetProperty("fix_pct").GetDouble();
        Assert.InRange(testPct, 0.59, 0.61);
        Assert.InRange(fixPct, 0.39, 0.41);
        Assert.InRange(testPct + fixPct, 0.999, 1.001);
    }

    [Fact(DisplayName = "summary: чистый цикл без регрессии → test_retest=null, rework_rate=0")]
    public async Task Summary_NoRegression_TestRetestNull_ReworkRateZero()
    {
        var now = DateTimeOffset.UtcNow;
        await SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "clean-cycle-r1",
            status: ReportStatus.Resolved,
            isExcluded: false,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-5), now.AddDays(-4), 0),
                new SeedInterval(ReportStatus.Fix,  now.AddDays(-4), now.AddDays(-3), 0),
            ]);

        var resp = await _client.GetAsync("/v2/analytics/summary?period=30d");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("reports_closed").GetInt32());
        Assert.Equal(0.0, root.GetProperty("rework_rate").GetDouble());
        Assert.Equal(JsonValueKind.Null,
            root.GetProperty("avg_regression_cycles_when_present").ValueKind);

        var avgPhase = root.GetProperty("avg_phase_duration_days");
        Assert.InRange(avgPhase.GetProperty("test_initial").GetDouble(), 0.99, 1.01);
        Assert.Equal(JsonValueKind.Null, avgPhase.GetProperty("test_retest").ValueKind);
        Assert.InRange(avgPhase.GetProperty("fix").GetDouble(), 0.99, 1.01);

        Assert.Equal(JsonValueKind.Array, root.GetProperty("top_regression_reports").ValueKind);
        Assert.Equal(0, root.GetProperty("top_regression_reports").GetArrayLength());
    }

    [Fact(DisplayName = "summary: phase_trends_weekly содержит две ISO-недели для репортов в разных неделях")]
    public async Task Summary_PhaseTrendsWeekly_IsoWeekBoundary()
    {
        // Два репорта, закрытые в соседних ISO-неделях (минимум 7 дней между closed_at
        // гарантирует, что date_trunc('week') попадает в разные недели).
        var now = DateTimeOffset.UtcNow;
        var weekAEnter = now.AddDays(-15);
        var weekAExit = now.AddDays(-14);
        var weekBEnter = now.AddDays(-8);
        var weekBExit = now.AddDays(-7);

        await SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "week-A",
            status: ReportStatus.Resolved,
            isExcluded: false,
            intervals: [new SeedInterval(ReportStatus.Test, weekAEnter, weekAExit, 0)]);

        await SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "week-B",
            status: ReportStatus.Resolved,
            isExcluded: false,
            intervals: [new SeedInterval(ReportStatus.Test, weekBEnter, weekBExit, 0)]);

        var resp = await _client.GetAsync("/v2/analytics/summary?period=360d");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var trends = doc.RootElement.GetProperty("phase_trends_weekly");

        Assert.True(trends.GetArrayLength() >= 2,
            $"Ожидалось >= 2 ISO-недель в phase_trends_weekly, получено {trends.GetArrayLength()}.");

        var labels = new List<string>();
        foreach (var t in trends.EnumerateArray())
        {
            var iso = t.GetProperty("iso_week").GetString()!;
            // Формат IYYY-Www: 4 цифры года, дефис, "W", 2 цифры номера недели.
            Assert.Matches(@"^\d{4}-W\d{2}$", iso);
            labels.Add(iso);
        }

        Assert.True(labels.Distinct().Count() >= 2,
            $"Ожидалось >= 2 различных ISO-недели, получено: [{string.Join(", ", labels)}].");
    }

    [Fact(DisplayName = "PATCH /v2/reports/{id} → следующий summary не учитывает репорт")]
    public async Task PatchToggle_ExcludesFromSubsequentSummary()
    {
        var now = DateTimeOffset.UtcNow;
        var reportId = await SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "to-exclude",
            status: ReportStatus.Resolved,
            isExcluded: false,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-5), now.AddDays(-4), 0),
                new SeedInterval(ReportStatus.Fix,  now.AddDays(-4), now.AddDays(-3), 0),
            ]);

        // Summary 1: репорт учитывается.
        var resp1 = await _client.GetAsync("/v2/analytics/summary?period=30d");
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        using (var doc1 = JsonDocument.Parse(await resp1.Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, doc1.RootElement.GetProperty("reports_closed").GetInt32());
        }

        // PATCH: исключаем.
        var patchResp = await _client.PatchAsJsonAsync(
            $"/v2/reports/{reportId}",
            new { is_excluded_from_analytics = true });
        Assert.Equal(HttpStatusCode.OK, patchResp.StatusCode);

        // Summary 2: репорт уже не в выборке.
        var resp2 = await _client.GetAsync("/v2/analytics/summary?period=30d");
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        using var doc2 = JsonDocument.Parse(await resp2.Content.ReadAsStringAsync());
        Assert.Equal(0, doc2.RootElement.GetProperty("reports_closed").GetInt32());
        Assert.Equal(0, doc2.RootElement.GetProperty("top_regression_reports").GetArrayLength());
    }

    [Fact(DisplayName = "report-detail: bugs_added_during_regression считает только баги из retest-интервалов")]
    public async Task ReportDetail_BugsAddedDuringRegression_OnlyRetestWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var initialTestStart = now.AddDays(-10);
        var initialTestEnd = now.AddDays(-9);
        var fixStart = now.AddDays(-9);
        var fixEnd = now.AddDays(-8);
        var retestStart = now.AddDays(-8);
        var retestEnd = now.AddDays(-7);

        var reportId = await SeedClosedReportAsync(
            workspaceId: _workspaceId,
            title: "regression-bug-window",
            status: ReportStatus.Resolved,
            isExcluded: false,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, initialTestStart, initialTestEnd, 0),
                new SeedInterval(ReportStatus.Fix,  fixStart,        fixEnd,         0),
                new SeedInterval(ReportStatus.Test, retestStart,     retestEnd,      1),
            ]);

        // Bug 1 — в initial Test: НЕ regression.
        await SeedBugAsync(reportId, BugStatus.Open, initialTestStart.AddHours(2));
        // Bug 2 — в Fix: НЕ regression.
        await SeedBugAsync(reportId, BugStatus.Fixed, fixStart.AddHours(3));
        // Bug 3, 4 — в retest: regression.
        await SeedBugAsync(reportId, BugStatus.Verified, retestStart.AddHours(1));
        await SeedBugAsync(reportId, BugStatus.Open, retestStart.AddHours(5));

        var resp = await _client.GetAsync($"/v2/reports/{reportId}/analytics");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("regression_cycles").GetInt32());
        Assert.Equal(2, root.GetProperty("bugs_added_during_regression").GetInt32());
    }

    // ============ Seed helpers ============

    private sealed record SeedInterval(
        ReportStatus Phase,
        DateTimeOffset EnteredAt,
        DateTimeOffset? ExitedAt,
        int RegressionCycleIndex);

    private async Task<int> SeedClosedReportAsync(
        string workspaceId,
        string title,
        ReportStatus status,
        bool isExcluded,
        SeedInterval[] intervals,
        string? creatorTeamId = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var reportId = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO public.reports (
                title, status, responsible_user_id, creator_user_id,
                created_at, updated_at, creator_organization_id, is_excluded_from_analytics,
                creator_team_id
            ) VALUES (
                @title, @status, '', 'seed-user',
                now(), now(), @workspaceId, @isExcluded,
                @creatorTeamId
            ) RETURNING id;",
            new
            {
                title,
                status = (int)status,
                workspaceId,
                isExcluded,
                creatorTeamId,
            });

        // source_event_id уникален глобально — генерим псевдо-уникальный bigint.
        // Префикс 2_000_000_000 разводит этот seeder с базовым (1_000_000),
        // чтобы коллизий не было даже при совпадении report_id.
        var seq = 0;
        foreach (var interval in intervals)
        {
            seq++;
            await conn.ExecuteAsync(@"
                INSERT INTO public.report_phase_intervals (
                    report_id, phase, entered_at, exited_at,
                    regression_cycle_index, source_event_id
                ) VALUES (
                    @reportId, @phase, @enteredAt, @exitedAt,
                    @regressionCycleIndex, @sourceEventId
                );",
                new
                {
                    reportId,
                    phase = (short)interval.Phase,
                    enteredAt = interval.EnteredAt,
                    exitedAt = interval.ExitedAt,
                    regressionCycleIndex = interval.RegressionCycleIndex,
                    sourceEventId = 2_000_000_000L + ((long)reportId * 1_000_000L) + seq,
                });
        }

        return reportId;
    }

    private async Task SeedBugAsync(int reportId, BugStatus status, DateTimeOffset createdAt)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO public.bugs (
                report_id, receive, expect, created_at, updated_at, creator_user_id, status
            ) VALUES (
                @reportId, 'r', 'e', @createdAt, @createdAt, 'seed-user', @status
            );",
            new { reportId, status = (int)status, createdAt });
    }

    /// <summary>
    /// Та же фикстура, что у <see cref="AnalyticsControllerTests"/>: header-based
    /// auth + общий Postgres-контейнер через <see cref="PostgresCollection"/>.
    /// </summary>
    public sealed class AnalyticsT16Fixture(PostgresContainerFixture container) : WebApplicationFactory<Program>
    {
        private readonly PostgreSqlContainer _db = container.Container;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", _db.GetConnectionString());
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "development");

            var fileStorageDir = Path.Combine(Path.GetTempPath(), "bugget-tests-analytics-t16-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fileStorageDir);
            builder.UseSetting("FileStorageOptions:BaseDirectory", fileStorageDir);

            builder.UseSetting("ExternalSettings:Authentication:UserIdHeaderName", UserHeader);
            builder.UseSetting("ExternalSettings:Authentication:OrganizationIdHeaderName", OrganizationHeader);

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<ITaskQueue>();
                services.AddSingleton<ITaskQueue>(sp => new SyncTaskQueue(sp));
            });
        }
    }
}
