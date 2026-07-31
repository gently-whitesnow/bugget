namespace Bugget.Domain.Analytics;

/// <summary>
/// Один интервал фазы из read-model <c>report_phase_intervals</c>.
/// Phase хранится как <c>short</c> совместимо с DDL миграции 039
/// (значение = <c>(short)ReportStatus.Test</c> или <c>(short)ReportStatus.Fix</c>).
/// </summary>
public sealed class PhaseIntervalBo
{
    public required int ReportId { get; init; }
    public required short Phase { get; init; }
    public required DateTimeOffset EnteredAt { get; init; }
    public DateTimeOffset? ExitedAt { get; init; }
    public required int RegressionCycleIndex { get; init; }
}
