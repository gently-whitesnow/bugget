using Bugget.Application.Analytics;
using Bugget.Domain.Analytics;

namespace Bugget.Application.Ports;

/// <summary>
/// Проекция интервалов фаз репорта: сколько времени репорт провёл в каждой фазе и
/// сколько раз в неё возвращался. Все методы принимают <see cref="ITransactionScope"/> —
/// проекция обновляется в той же транзакции, в которой poller продвигает курсор событий.
/// </summary>
public interface IReportPhaseIntervalsDbClient
{
    /// <summary>
    /// Сколько раз репорт уже выходил из указанной фазы. По этому числу нумеруется
    /// заход в фазу: 0 — первичный, 1 — первый повтор и так далее.
    /// </summary>
    Task<int> CountClosedIntervalsAsync(
        ITransactionScope scope,
        int reportId,
        short phase,
        CancellationToken ct);

    /// <summary>
    /// Закрывает открытый интервал репорта моментом события. Повторная доставка события
    /// ничего не меняет: интервал не закрывается ни задним числом
    /// (<paramref name="exitedAt"/> раньше момента его открытия), ни тем самым событием,
    /// которым он был открыт (<paramref name="currentEventId"/>).
    /// </summary>
    /// <returns>Сколько интервалов закрылось: 0 — обработка была повторной.</returns>
    Task<int> CloseActiveIntervalAsync(
        ITransactionScope scope,
        int reportId,
        DateTimeOffset exitedAt,
        long currentEventId,
        CancellationToken ct);

    /// <summary>
    /// Открывает интервал фазы. Операция идемпотентна по событию-источнику: повторная
    /// доставка того же события второй интервал не создаёт.
    /// </summary>
    /// <returns>Сколько интервалов открылось: 0 — событие уже было обработано.</returns>
    Task<int> OpenIntervalAsync(
        ITransactionScope scope,
        OpenReportPhaseIntervalCommand command,
        CancellationToken ct);
}
