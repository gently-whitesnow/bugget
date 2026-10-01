namespace Bugget.Application.DomainEvents.Consumer;

/// <summary>
/// Секция <c>DomainEventsConsumer</c>. Один <see cref="ConsumerName"/> = одна позиция в <c>domain_events_cursor</c>;
/// все <see cref="IDomainEventHandler"/> делят один cursor, диспатч по EventType.
/// </summary>
public sealed class DomainEventsConsumerOptions
{
    /// <summary>Должно совпадать с seed'ом миграции <c>domain_events_cursor</c>.</summary>
    public string ConsumerName { get; init; } = "bugget-analytics";

    /// <summary>Пауза между холостыми тиками (пустой batch / отсутствие событий).</summary>
    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Пауза после неуспешного тика (исключение на уровне poller, не handler'а).</summary>
    public TimeSpan ErrorBackoff { get; init; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; init; } = 100;
}
