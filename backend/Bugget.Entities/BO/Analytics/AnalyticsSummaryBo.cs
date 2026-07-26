namespace Bugget.Entities.BO.Analytics;

/// <summary>
/// BO-представление сводной аналитики. Маппится в контрактный
/// <c>AnalyticsSummary</c> в контроллере. Все длительности — в днях.
/// </summary>
public sealed class AnalyticsSummaryBo
{
    public required PeriodWindow Period { get; init; }
    public double AvgTestInitialDays { get; init; }
    public double? AvgTestRetestDays { get; init; }
    public double? AvgFixDays { get; init; }
    public double? AvgFullCycleDays { get; init; }
    public double ReworkRate { get; init; }
    public double? AvgRegressionCyclesWhenPresent { get; init; }
    public int ReportsClosed { get; init; }
    public double TestPct { get; init; }
    public double FixPct { get; init; }
    public required IReadOnlyList<TopRegressionReportBo> TopRegressionReports { get; init; }
    public required IReadOnlyList<PhaseTrendWeeklyBo> PhaseTrendsWeekly { get; init; }
}

public sealed class TopRegressionReportBo
{
    public required long ReportId { get; init; }
    public required string Title { get; init; }
    public required int RegressionCycles { get; init; }
}

public sealed class PhaseTrendWeeklyBo
{
    public required string IsoWeek { get; init; }
    public required double TestDays { get; init; }
    public required double FixDays { get; init; }
    public required int ReportsClosed { get; init; }
}
