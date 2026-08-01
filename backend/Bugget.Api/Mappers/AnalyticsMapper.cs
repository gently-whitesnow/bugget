using Bugget.Contracts.Analytics.Generated;
using Bugget.Domain.Analytics;
using Bugget.Domain.Reports;
using AnalyticsPhaseName = Bugget.Contracts.Analytics.Generated.PhaseName;
using ReportContracts = Bugget.Contracts.Reports.Generated;

namespace Bugget.Api.Mappers;

/// <summary>
/// BO → Contracts маппер для эндпоинтов <c>/v2/analytics/*</c> + sub-resource
/// <c>/v2/reports/{id}/analytics</c>. Контрактные DTO видны только в Bugget,
/// поэтому маппер живёт в Web-проекте, не в Bugget.Api.BO.
///
/// Особенность: после R6 detail-DTO (`AnalyticsReport*`) переехали в
/// <see cref="ReportContracts"/> (модуль reports), а summary/responsible-DTO
/// остались в <c>Bugget.Contracts.Analytics.Generated</c>. Алиасим оба
/// namespaces, чтобы не вводить ambiguity.
/// </summary>
internal static class AnalyticsMapper
{
    public static Period ToContract(this PeriodWindow window) => new()
    {
        From = window.From,
        To = window.To,
        Label = window.Label,
    };

    public static AnalyticsSummary ToContract(this AnalyticsSummaryBo bo)
    {
        var dto = new AnalyticsSummary
        {
            Period = bo.Period.ToContract(),
            Avg_phase_duration_days = new AvgPhaseDurationDays
            {
                Test_initial = bo.AvgTestInitialDays,
                Test_retest = bo.AvgTestRetestDays,
                Fix = bo.AvgFixDays,
            },
            Avg_full_cycle_days = bo.AvgFullCycleDays,
            Rework_rate = bo.ReworkRate,
            Avg_regression_cycles_when_present = bo.AvgRegressionCyclesWhenPresent,
            Reports_closed = bo.ReportsClosed,
            Phase_time_distribution = new PhaseTimeDistribution
            {
                Test_pct = bo.TestPct,
                Fix_pct = bo.FixPct,
            },
        };

        // NSwag DTO держит коллекции как get-only IReadOnlyList, а инстанциирует
        // их как List — каст к List<T> безопасный.
        var topList = (List<TopRegressionReport>)dto.Top_regression_reports;
        foreach (var top in bo.TopRegressionReports)
        {
            topList.Add(new TopRegressionReport
            {
                Report_id = top.ReportId,
                Title = top.Title,
                Regression_cycles = top.RegressionCycles,
            });
        }

        var trendList = (List<PhaseTrendWeekly>)dto.Phase_trends_weekly;
        foreach (var trend in bo.PhaseTrendsWeekly)
        {
            trendList.Add(new PhaseTrendWeekly
            {
                Iso_week = trend.IsoWeek,
                Test_days = trend.TestDays,
                Fix_days = trend.FixDays,
                Reports_closed = trend.ReportsClosed,
            });
        }

        return dto;
    }

    public static ReportContracts.AnalyticsReport ToContract(this AnalyticsReportBo bo)
    {
        var dto = new ReportContracts.AnalyticsReport
        {
            Report_id = bo.ReportId,
            Regression_cycles = bo.RegressionCycles,
            Bugs_by_status = new ReportContracts.AnalyticsReportBugsByStatus
            {
                Open = bo.BugsByStatus.Open,
                Fixed = bo.BugsByStatus.Fixed,
                Verified = bo.BugsByStatus.Verified,
                Rejected = bo.BugsByStatus.Rejected,
            },
            Bugs_added_during_regression = bo.BugsAddedDuringRegression,
        };

        var timelineList = (List<ReportContracts.AnalyticsReportPhaseEntry>)dto.Phase_timeline;
        foreach (var phase in bo.PhaseTimeline)
        {
            timelineList.Add(new ReportContracts.AnalyticsReportPhaseEntry
            {
                Phase = MapReportPhase(phase.Phase),
                Entered_at = phase.EnteredAt,
                Exited_at = phase.ExitedAt,
                Duration_days = phase.ExitedAt.HasValue
                    ? (phase.ExitedAt.Value - phase.EnteredAt).TotalDays
                    : null,
                Regression_cycle_index = phase.RegressionCycleIndex,
            });
        }

        return dto;
    }

    private static AnalyticsPhaseName MapAnalyticsPhase(short phase) => phase switch
    {
        (short)ReportStatus.Test => AnalyticsPhaseName.Test,
        (short)ReportStatus.Fix => AnalyticsPhaseName.Fix,
        _ => throw new InvalidOperationException(
            $"Unexpected phase value in report_phase_intervals: {phase}"),
    };

    private static ReportContracts.PhaseName MapReportPhase(short phase) => phase switch
    {
        (short)ReportStatus.Test => ReportContracts.PhaseName.Test,
        (short)ReportStatus.Fix => ReportContracts.PhaseName.Fix,
        _ => throw new InvalidOperationException(
            $"Unexpected phase value in report_phase_intervals: {phase}"),
    };

    public static AnalyticsResponsible ToContract(this AnalyticsResponsibleBo bo)
    {
        var dto = new AnalyticsResponsible
        {
            Period = bo.Period.ToContract(),
            Avg_fix_phase_days = bo.AvgFixPhaseDays,
        };

        var participatedList = (List<AnalyticsResponsibleParticipatedReport>)dto.Reports_participated;
        foreach (var r in bo.ReportsParticipated)
        {
            participatedList.Add(new AnalyticsResponsibleParticipatedReport
            {
                Report_id = r.ReportId,
                Title = r.Title,
                Current_phase = MapAnalyticsPhase(r.CurrentPhase),
            });
        }

        var completedList = (List<AnalyticsResponsibleCompletedReport>)dto.Reports_completed;
        foreach (var r in bo.ReportsCompleted)
        {
            completedList.Add(new AnalyticsResponsibleCompletedReport
            {
                Report_id = r.ReportId,
                Title = r.Title,
                Closed_at = r.ClosedAt,
                Outcome = MapOutcome(r.Outcome),
            });
        }

        return dto;
    }

    private static ResponsibleOutcome MapOutcome(short status) => status switch
    {
        (short)ReportStatus.Resolved => ResponsibleOutcome.Resolved,
        (short)ReportStatus.Rejected => ResponsibleOutcome.Rejected,
        _ => throw new InvalidOperationException(
            $"Unexpected outcome status for responsible: {status}"),
    };
}
