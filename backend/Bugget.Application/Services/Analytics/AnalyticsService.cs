using Bugget.Application.Ports;
using Bugget.Domain.Analytics;

namespace Bugget.Application.Services.Analytics;

/// <summary>
/// Бизнес-логика эндпоинтов <c>/v2/analytics/*</c> + sub-resource
/// <c>GET /v2/reports/{id}/analytics</c>: оркестрирует выборку данных через
/// <see cref="IAnalyticsDbClient"/> и сводит их в BO через pure-функцию
/// <see cref="ComputeSummary"/>.
/// </summary>
public sealed class AnalyticsService(IAnalyticsDbClient analyticsDb, TimeProvider timeProvider)
{
    /// <summary>
    /// Сводка по workspace. <paramref name="teamId"/> — опциональный фильтр по
    /// <c>reports.creator_team_id</c>; <c>null</c> → workspace-wide.
    /// Объединяет прежние <c>GetSummaryAsync</c> и <c>GetSummaryByTeamAsync</c>.
    /// </summary>
    public async Task<AnalyticsSummaryBo> GetSummaryAsync(
        string workspaceId,
        string? period,
        string? teamId,
        CancellationToken ct)
    {
        var window = PeriodResolver.Resolve(period, timeProvider.GetUtcNow());
        var raw = await analyticsDb.GetSummaryDataAsync(workspaceId, teamId, window.From, window.To, ct);
        return ComputeSummary(window, raw);
    }

    /// <summary>
    /// Сводка по конкретному <paramref name="userId"/>: participated + completed
    /// + avg_fix_phase_days.
    /// </summary>
    public async Task<AnalyticsResponsibleBo> GetByResponsibleAsync(
        string workspaceId,
        string userId,
        string? period,
        CancellationToken ct)
    {
        var window = PeriodResolver.Resolve(period, timeProvider.GetUtcNow());
        var raw = await analyticsDb.GetResponsibleDataAsync(workspaceId, userId, window.From, window.To, ct);
        return new AnalyticsResponsibleBo
        {
            Period = window,
            ReportsParticipated = raw.Participated,
            ReportsCompleted = raw.Completed,
            AvgFixPhaseDays = raw.AvgFixPhaseDays,
        };
    }

    public async Task<AnalyticsReportBo?> GetReportAsync(
        string workspaceId,
        long reportId,
        CancellationToken ct)
    {
        var timeline = await analyticsDb.GetReportTimelineAsync(workspaceId, reportId, ct);
        if (timeline is null)
        {
            return null;
        }

        var bugsByStatus = await analyticsDb.GetBugsByStatusAsync((int)reportId, ct);
        var bugsAddedDuringRegression =
            await analyticsDb.GetBugsAddedDuringRegressionAsync((int)reportId, ct);

        var testIntervals = timeline.Count(i => i.Phase == (short)Bugget.Domain.Reports.ReportStatus.Test);
        var regressionCycles = testIntervals > 0 ? testIntervals - 1 : 0;

        return new AnalyticsReportBo
        {
            ReportId = reportId,
            PhaseTimeline = timeline,
            RegressionCycles = regressionCycles,
            BugsByStatus = bugsByStatus,
            BugsAddedDuringRegression = bugsAddedDuringRegression,
        };
    }

    /// <summary>
    /// Pure-функция: собирает <see cref="AnalyticsSummaryBo"/> из «сырых» данных.
    /// Conditional denominator: TestRetest/Fix → null, если репортов с такой фазой нет.
    /// На пустой выборке: rework_rate = 0, avg_*/top — null/пусто.
    /// </summary>
    public static AnalyticsSummaryBo ComputeSummary(PeriodWindow window, AnalyticsRawData raw)
    {
        var closedReports = raw.ClosedReports;
        var totalClosed = closedReports.Count;

        var byBucket = raw.PhaseAggregates.ToDictionary(p => p.Bucket);

        double testInitialDays = AverageDays(byBucket, PhaseBucket.TestInitial) ?? 0.0;
        double? testRetestDays = AverageDays(byBucket, PhaseBucket.TestRetest);
        double? fixDays = AverageDays(byBucket, PhaseBucket.Fix);

        // Репорт закрыт из Backlog в Resolved → first_test_entered_at пустой; пропускаем.
        var fullCycleSamples = closedReports
            .Where(r => r.FirstTestEnteredAt.HasValue)
            .Select(r => (r.ClosedAt - r.FirstTestEnteredAt!.Value).TotalDays)
            .ToArray();
        double? avgFullCycleDays = fullCycleSamples.Length > 0 ? fullCycleSamples.Average() : null;

        var reportsWithRegression = closedReports.Count(r => r.TestIntervals >= 2);
        double reworkRate = totalClosed == 0 ? 0.0 : (double)reportsWithRegression / totalClosed;

        double? avgRegressionCyclesWhenPresent = reportsWithRegression == 0
            ? null
            : closedReports
                .Where(r => r.TestIntervals >= 2)
                .Average(r => (double)(r.TestIntervals - 1));

        long totalTestSeconds =
            (byBucket.GetValueOrDefault(PhaseBucket.TestInitial)?.TotalDurationSeconds ?? 0)
            + (byBucket.GetValueOrDefault(PhaseBucket.TestRetest)?.TotalDurationSeconds ?? 0);
        long totalFixSeconds =
            byBucket.GetValueOrDefault(PhaseBucket.Fix)?.TotalDurationSeconds ?? 0;
        long totalBoth = totalTestSeconds + totalFixSeconds;

        double testPct = totalBoth == 0 ? 0.0 : (double)totalTestSeconds / totalBoth;
        double fixPct = totalBoth == 0 ? 0.0 : (double)totalFixSeconds / totalBoth;

        var top = closedReports
            .Where(r => r.TestIntervals >= 2)
            .OrderByDescending(r => r.TestIntervals - 1)
            .ThenBy(r => r.ReportId)
            .Take(10)
            .Select(r => new TopRegressionReportBo
            {
                ReportId = r.ReportId,
                Title = r.Title,
                RegressionCycles = r.TestIntervals - 1,
            })
            .ToArray();

        return new AnalyticsSummaryBo
        {
            Period = window,
            AvgTestInitialDays = testInitialDays,
            AvgTestRetestDays = testRetestDays,
            AvgFixDays = fixDays,
            AvgFullCycleDays = avgFullCycleDays,
            ReworkRate = reworkRate,
            AvgRegressionCyclesWhenPresent = avgRegressionCyclesWhenPresent,
            ReportsClosed = totalClosed,
            TestPct = testPct,
            FixPct = fixPct,
            TopRegressionReports = top,
            PhaseTrendsWeekly = raw.PhaseTrendsWeekly,
        };
    }

    private static double? AverageDays(
        IReadOnlyDictionary<PhaseBucket, PhaseAggregateRow> byBucket,
        PhaseBucket bucket)
    {
        if (!byBucket.TryGetValue(bucket, out var row) || row.ReportCount == 0)
        {
            return null;
        }

        return row.TotalDurationSeconds / 86400.0 / row.ReportCount;
    }
}
