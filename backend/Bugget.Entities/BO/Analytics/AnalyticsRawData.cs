namespace Bugget.Entities.BO.Analytics;

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

/// <summary>
/// Группированные суммы duration/count по «семантической» фазе:
/// <list type="bullet">
///   <item>TestInitial — Test-интервалы с regression_cycle_index = 0;</item>
///   <item>TestRetest — Test-интервалы с regression_cycle_index ≥ 1;</item>
///   <item>Fix — все Fix-интервалы.</item>
/// </list>
/// Учитываются только репорты, закрытые в окне периода
/// (<c>is_excluded_from_analytics = FALSE</c>).
/// </summary>
public sealed class PhaseAggregateRow
{
    public required PhaseBucket Bucket { get; init; }
    public required int ReportCount { get; init; }
    public required long TotalDurationSeconds { get; init; }
}

public enum PhaseBucket
{
    TestInitial = 0,
    TestRetest = 1,
    Fix = 2,
}

/// <summary>
/// Сырой снимок данных <c>/v2/analytics/responsible/{userId}</c>.
/// Сборка в <see cref="AnalyticsResponsibleBo"/> идёт в <c>AnalyticsService</c>.
/// </summary>
public sealed class AnalyticsResponsibleRawData
{
    public required IReadOnlyList<ResponsibleParticipatedReportBo> Participated { get; init; }
    public required IReadOnlyList<ResponsibleCompletedReportBo> Completed { get; init; }
    public double? AvgFixPhaseDays { get; init; }
}
