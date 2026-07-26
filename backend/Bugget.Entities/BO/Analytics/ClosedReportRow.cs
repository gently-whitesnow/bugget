namespace Bugget.Entities.BO.Analytics;

/// <summary>
/// «Заголовок» закрытого в окне периода репорта: используется для
/// агрегатов summary (avg_full_cycle_days, rework_rate, regression cycles
/// и т.п.). Поле <c>ClosedAt</c> — момент перехода в терминальный статус
/// (Resolved/Rejected), вычисляется как <c>MAX(exited_at)</c> по интервалам
/// репорта (см. <c>AnalyticsDbClient</c>).
/// </summary>
public sealed class ClosedReportRow
{
    public required int ReportId { get; init; }
    public required string Title { get; init; }
    public required DateTimeOffset ClosedAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? FirstTestEnteredAt { get; init; }
    public int TestIntervals { get; init; }
    public int FixIntervals { get; init; }
    public long TestDurationSeconds { get; init; }
    public long FixDurationSeconds { get; init; }
}
