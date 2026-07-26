namespace Bugget.BO.DomainEvents.Consumer;

/// <summary>
/// Конфигурация локального outbox-консьюмера. Биндинг — секция
/// <c>DomainEventsConsumer</c> в <c>appsettings.json</c>. Один <see cref="ConsumerName"/>
/// = одна позиция в <c>domain_events_cursor</c>; все зарегистрированные
/// <see cref="IDomainEventHandler"/> делят один cursor, диспатч по EventType.
/// </summary>
public sealed class DomainEventsConsumerOptions
{
    /// <summary>
    /// Имя консьюмера в таблице <c>domain_events_cursor</c>; должно совпадать с
    /// seed'ом миграции.
    /// </summary>
    public string ConsumerName { get; init; } = "bugget-analytics";

    /// <summary>Пауза между холостыми тиками (пустой batch / отсутствие событий).</summary>
    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Пауза после неуспешного тика (исключение на уровне poller, не handler'а).</summary>
    public TimeSpan ErrorBackoff { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Размер batch'а, тянущегося одной выборкой из <c>domain_events</c>.</summary>
    public int BatchSize { get; init; } = 100;
}
