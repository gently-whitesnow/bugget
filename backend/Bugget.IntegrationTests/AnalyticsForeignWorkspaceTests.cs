using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bugget.Entities.BO.ReportBo;
using Dapper;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

/// <summary>
/// Workspace-isolation тесты для analytics-эндпоинтов (P1.1 review):
/// чужой workspace не должен утекать в summary (включая summary?teamId) / responsible.
/// Плюс P1.2 — race condition на PATCH `is_excluded_from_analytics`
/// (FOR UPDATE row lock в <c>GetIsExcludedFromAnalyticsAsync</c>
/// гарантирует ровно одно событие).
///
/// Вынесены в отдельный класс, чтобы не утолщать
/// <see cref="AnalyticsControllerTests"/> сверх ratchet'а maintainability
/// (legacy typeMaxLoc = 500). Шарят AnalyticsAppFixture с базовым набором тестов.
/// </summary>
[Collection("PostgresCollection")]
public sealed class AnalyticsForeignWorkspaceTests
    : IClassFixture<AnalyticsControllerTests.AnalyticsAppFixture>
{
    private const string OrganizationHeader = "X-Organization-Id";
    private const string UserHeader = "X-User-Id";

    private readonly HttpClient _client;
    private readonly string _connectionString;
    private readonly string _workspaceId;

    public AnalyticsForeignWorkspaceTests(AnalyticsControllerTests.AnalyticsAppFixture fixture)
    {
        _workspaceId = $"ws_{Guid.NewGuid():N}";
        _client = fixture.CreateClient();
        _client.DefaultRequestHeaders.Add(OrganizationHeader, _workspaceId);
        _client.DefaultRequestHeaders.Add(UserHeader, "test-user");
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
    }

    [Fact(DisplayName = "GET /v2/analytics/summary: репорт из чужого workspace полностью исключён")]
    public async Task Summary_ForeignWorkspaceReport_ExcludedFromOwnSummary()
    {
        // P1.1: workspace-фильтр в SQL полностью отсекает чужие репорты —
        // ни в reports_closed, ни в top_regression_reports.
        var foreignWorkspace = $"foreign_ws_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        // Чужой репорт с регрессией (≥2 Test интервала) — кандидат на top.
        var foreignReportId = await SeedClosedReportAsync(
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
        // P1.1: даже если teamId принадлежит чужому workspace и в чужом workspace
        // есть репорты этой команды — наш workspace их не видит (workspace-guard в SQL).
        var foreignWorkspace = $"foreign_ws_{Guid.NewGuid():N}";
        const long foreignTeamId = 7777L;
        var now = DateTimeOffset.UtcNow;

        var foreignReportId = await SeedClosedReportAsync(
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
        // P1.1: userId, который участвует только в чужих репортах, не должен
        // утечь в /responsible для нашего workspace — оба списка пустые.
        var foreignWorkspace = $"foreign_ws_{Guid.NewGuid():N}";
        var foreignUserId = $"foreign_user_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        // Активный foreign-репорт.
        var foreignActive = await SeedClosedReportAsync(
            workspaceId: foreignWorkspace,
            title: "foreign-active",
            status: ReportStatus.Test,
            isExcluded: false,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-3), null, 0),
            ]);
        await SeedParticipantAsync(foreignActive, foreignUserId);

        // Closed foreign-репорт.
        var foreignCompleted = await SeedClosedReportAsync(
            workspaceId: foreignWorkspace,
            title: "foreign-completed",
            status: ReportStatus.Resolved,
            isExcluded: false,
            intervals:
            [
                new SeedInterval(ReportStatus.Test, now.AddDays(-5), now.AddDays(-4), 0),
                new SeedInterval(ReportStatus.Fix,  now.AddDays(-4), now.AddDays(-3), 0),
            ]);
        await SeedParticipantAsync(foreignCompleted, foreignUserId);

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
        // P1.2 review: гонка concurrent PATCH-toggle.
        // Без FOR UPDATE два параллельных PATCH (false→true) могли прочитать
        // одно и то же `false`, оба считать это переходом и оба эмитнуть событие.
        // С FOR UPDATE row lock второй PATCH ждёт первый, читает уже `true`,
        // фиксирует no-op и не эмитит дубль.
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

        // 5 параллельных PATCH с одним и тем же значением `true`.
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

        // Главное: ровно одно событие — FOR UPDATE сериализовал PATCH'и.
        var eventCount = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)::int FROM public.domain_events
            WHERE aggregate_id = @aggId
              AND event_type = 'bugget.report.excluded_from_analytics_toggled';",
            new { aggId = reportId.ToString() });
        Assert.Equal(1, eventCount);
    }

    // ============ Seed helpers (дубль базового набора в AnalyticsControllerTests) ============

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
                    sourceEventId = ((long)reportId * 1_000_000L) + seq,
                });
        }

        return reportId;
    }

    private async Task SeedParticipantAsync(int reportId, string userId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO public.report_participants (report_id, user_id)
            VALUES (@reportId, @userId)
            ON CONFLICT DO NOTHING;",
            new { reportId, userId });
    }
}
