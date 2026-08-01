namespace Bugget.Domain.Analytics;

/// <summary>
/// BO-представление детальной фазовой аналитики одного репорта.
/// Маппится в контрактный <c>AnalyticsReport</c> в контроллере.
/// </summary>
public sealed class AnalyticsReportBo
{
    public required long ReportId { get; init; }
    public required IReadOnlyList<PhaseIntervalBo> PhaseTimeline { get; init; }
    public required int RegressionCycles { get; init; }
    public required BugsByStatusBo BugsByStatus { get; init; }
    public required int BugsAddedDuringRegression { get; init; }
}
