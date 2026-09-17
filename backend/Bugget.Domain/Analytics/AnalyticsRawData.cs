namespace Bugget.Domain.Analytics;

/// <summary>
/// «Сырой» снимок данных summary, собранный <see cref="ClosedReportRow"/> + еженедельные тренды.
/// Возвращается из <c>AnalyticsDbClient.GetSummaryDataAsync</c> и преобразуется
/// в <see cref="AnalyticsSummaryBo"/> сервисом <c>AnalyticsService</c>.
/// </summary>
public sealed class AnalyticsRawData
{
    public required IReadOnlyList<ClosedReportRow> ClosedReports { get; init; }
    public required IReadOnlyList<PhaseAggregateRow> PhaseAggregates { get; init; }
    public required IReadOnlyList<PhaseTrendWeeklyBo> PhaseTrendsWeekly { get; init; }
}
