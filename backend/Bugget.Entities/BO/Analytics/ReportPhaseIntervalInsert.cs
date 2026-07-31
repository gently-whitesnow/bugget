namespace Bugget.Entities.BO.Analytics;

/// <summary>
/// Параметры INSERT'а в <c>report_phase_intervals</c> (TECHSPEC §3.1).
/// Проекция строится handler'ом из событий <c>bugget.report.status_changed</c>;
/// <see cref="SourceEventId"/> — id события из <c>domain_events</c>, гарантирует
/// идемпотентность через UNIQUE-индекс.
/// </summary>
public sealed class ReportPhaseIntervalInsert
{
    public required int ReportId { get; init; }
    public required short Phase { get; init; }
    public required DateTimeOffset EnteredAt { get; init; }
    public required int RegressionCycleIndex { get; init; }
    public required long SourceEventId { get; init; }
}
