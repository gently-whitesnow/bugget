using System.Data;
using Bugget.Entities.DbModels.Analytics;

namespace Bugget.DA.Interfaces;

/// <summary>
/// Доступ к read-model <c>report_phase_intervals</c>. Все методы принимают
/// <see cref="IDbConnection"/> + <see cref="IDbTransaction"/> — операции
/// выполняются внутри транзакции poller'а.
/// </summary>
public interface IReportPhaseIntervalsDbClient
{
    /// <summary>
    /// Считает количество закрытых интервалов указанной фазы у репорта; используется
    /// для <c>regression_cycle_index</c> при открытии нового интервала
    /// (0 = первичный заход, 1 = первый ретест и т.д.).
    /// </summary>
    Task<int> CountClosedIntervalsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        int reportId,
        short phase,
        CancellationToken ct);

    /// <summary>
    /// Закрывает активный (<c>exited_at IS NULL</c>) интервал репорта датой события.
    /// Идемпотентность при replay: апдейт пропускается, если активный интервал открыт
    /// позже <paramref name="exitedAt"/> (inversion) или тем же событием
    /// (<paramref name="currentEventId"/>, self-close при replay).
    /// </summary>
    Task<int> CloseActiveIntervalAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        int reportId,
        DateTimeOffset exitedAt,
        long currentEventId,
        CancellationToken ct);

    /// <summary>
    /// Открывает новый интервал. <c>INSERT … ON CONFLICT (source_event_id) DO NOTHING</c> —
    /// идемпотентность при повторной доставке события.
    /// </summary>
    Task<int> InsertIntervalAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        ReportPhaseIntervalInsertDbModel row,
        CancellationToken ct);
}
