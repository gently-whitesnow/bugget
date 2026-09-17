namespace Bugget.Domain.Analytics;

/// <summary>
/// Суммы duration/count по фазе: TestInitial — Test с regression_cycle_index = 0, TestRetest — с индексом ≥ 1,
/// Fix — все Fix-интервалы. Только репорты, закрытые в окне периода (<c>is_excluded_from_analytics = FALSE</c>).
/// </summary>
public sealed class PhaseAggregateRow
{
    public required PhaseBucket Bucket { get; init; }
    public required int ReportCount { get; init; }
    public required long TotalDurationSeconds { get; init; }
}
