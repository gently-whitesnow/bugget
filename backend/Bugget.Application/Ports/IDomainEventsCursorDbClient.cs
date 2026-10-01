namespace Bugget.Application.Ports;

/// <summary>Доступ к таблице <c>domain_events_cursor</c> для локального outbox-консьюмера.</summary>
public interface IDomainEventsCursorDbClient
{
    /// <summary>Текущая позиция консьюмера. <c>null</c> если строки нет.</summary>
    Task<long?> GetAsync(string consumerName, CancellationToken ct);

    /// <summary>Bootstrap-init cursor'а (INSERT-or-no-op): true, если строка вставлена.</summary>
    Task<bool> TryInitAsync(string consumerName, long initialEventId, CancellationToken ct);

    /// <summary>
    /// Двигает cursor вперёд внутри переданной транзакции; <c>WHERE last_event_id &lt; @newId</c> —
    /// monotonic guard от отката назад. Возвращает число затронутых строк.
    /// </summary>
    Task<int> UpdateAsync(
        string consumerName,
        long newLastEventId,
        ITransactionScope scope,
        CancellationToken ct);
}
