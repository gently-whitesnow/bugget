namespace Bugget.BO.Ports;

/// <summary>
/// Доступ к таблице <c>domain_events_cursor</c> для локального outbox-консьюмера.
/// </summary>
public interface IDomainEventsCursorClient
{
    /// <summary>Текущая позиция консьюмера. <c>null</c> если строки нет.</summary>
    Task<long?> GetAsync(string consumerName, CancellationToken ct);

    /// <summary>
    /// INSERT-or-no-op: bootstrap-init cursor'а в дефолтную позицию. Возвращает true,
    /// если строка была вставлена; false — если уже существовала.
    /// </summary>
    Task<bool> TryInitAsync(string consumerName, long initialEventId, CancellationToken ct);

    /// <summary>
    /// Двигает cursor вперёд внутри переданной транзакции.
    /// <c>WHERE last_event_id &lt; @newId</c> — monotonic guard, защищает от случайного
    /// отката cursor'а назад. Возвращает число затронутых строк.
    /// </summary>
    Task<int> UpdateAsync(
        string consumerName,
        long newLastEventId,
        ITransactionScope scope,
        CancellationToken ct);
}
