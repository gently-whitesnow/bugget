using Bugget.Application.Ports;
using Bugget.Domain.Analytics;

namespace Bugget.Application.Services.Analytics;

/// <summary>
/// Логика <c>/v2/analytics/*</c> и <c>GET /v2/reports/{id}/analytics</c>: выборка через
/// <see cref="IAnalyticsDbClient"/>, сведение в BO — pure-функцией <see cref="ComputeSummary"/>.
/// </summary>
public sealed class AnalyticsService(IAnalyticsDbClient analyticsDb, TimeProvider timeProvider) : IAnalyticsService
{
    /// <summary>Сводка по workspace; teamId — опциональный фильтр по <c>reports.creator_team_id</c>, <c>null</c> → workspace-wide.</summary>
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

    // Pure-функция. Conditional denominator: TestRetest/Fix → null, если репортов с такой фазой нет.
    // На пустой выборке: rework_rate = 0, avg_*/top — null/пусто.
    public static AnalyticsSummaryBo ComputeSummary(PeriodWindow window, AnalyticsRawData raw)
    {
        var closedReports = raw.ClosedReports;
        var totalClosed = closedReports.Count;

        var byBucket = raw.PhaseAggregates.ToDictionary(p => p.Bucket);

        var testInitialDays = AverageDays(byBucket, PhaseBucket.TestInitial) ?? 0.0;
        var testRetestDays = AverageDays(byBucket, PhaseBucket.TestRetest);
        var fixDays = AverageDays(byBucket, PhaseBucket.Fix);

        // Репорт закрыт из Backlog в Resolved → first_test_entered_at пустой; пропускаем.
        var fullCycleSamples = closedReports
            .Where(r => r.FirstTestEnteredAt.HasValue)
            .Select(r => (r.ClosedAt - r.FirstTestEnteredAt!.Value).TotalDays)
            .ToArray();
        double? avgFullCycleDays = fullCycleSamples.Length > 0 ? fullCycleSamples.Average() : null;

        var reportsWithRegression = closedReports.Count(r => r.TestIntervals >= 2);
        var reworkRate = totalClosed == 0 ? 0.0 : (double)reportsWithRegression / totalClosed;

        double? avgRegressionCyclesWhenPresent = reportsWithRegression == 0
            ? null
            : closedReports
                .Where(r => r.TestIntervals >= 2)
                .Average(r => (double)(r.TestIntervals - 1));

        var totalTestSeconds =
            (byBucket.GetValueOrDefault(PhaseBucket.TestInitial)?.TotalDurationSeconds ?? 0)
            + (byBucket.GetValueOrDefault(PhaseBucket.TestRetest)?.TotalDurationSeconds ?? 0);
        var totalFixSeconds =
            byBucket.GetValueOrDefault(PhaseBucket.Fix)?.TotalDurationSeconds ?? 0;
        var totalBoth = totalTestSeconds + totalFixSeconds;

        var testPct = totalBoth == 0 ? 0.0 : (double)totalTestSeconds / totalBoth;
        var fixPct = totalBoth == 0 ? 0.0 : (double)totalFixSeconds / totalBoth;

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
        Dictionary<PhaseBucket, PhaseAggregateRow> byBucket,
        PhaseBucket bucket)
    {
        if (!byBucket.TryGetValue(bucket, out var row) || row.ReportCount == 0)
        {
            return null;
        }

        return row.TotalDurationSeconds / 86400.0 / row.ReportCount;
    }
}
