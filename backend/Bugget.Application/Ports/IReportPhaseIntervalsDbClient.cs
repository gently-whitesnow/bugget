using Bugget.Application.Analytics;
using Bugget.Domain.Analytics;

namespace Bugget.Application.Ports;

/// <summary>
/// Проекция интервалов фаз репорта. Все методы принимают <see cref="ITransactionScope"/>:
/// проекция обновляется в той же транзакции, в которой poller продвигает курсор событий.
/// </summary>
public interface IReportPhaseIntervalsDbClient
{
    /// <summary>Число выходов репорта из фазы; им нумеруется заход: 0 — первичный, 1 — первый повтор.</summary>
    Task<int> CountClosedIntervalsAsync(
        ITransactionScope scope,
        int reportId,
        short phase,
        CancellationToken ct);

    /// <summary>Закрывает открытый интервал моментом события; 0 — повторная доставка.</summary>
    /// <remarks>Интервал не закрывается ни задним числом, ни тем же событием, которым был открыт.</remarks>
    Task<int> CloseActiveIntervalAsync(
        ITransactionScope scope,
        int reportId,
        DateTimeOffset exitedAt,
        long currentEventId,
        CancellationToken ct);

    /// <summary>Открывает интервал; идемпотентно по событию-источнику (0 — событие уже обработано).</summary>
    Task<int> OpenIntervalAsync(
        ITransactionScope scope,
        OpenReportPhaseIntervalCommand command,
        CancellationToken ct);
}
