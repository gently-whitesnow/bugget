using Bugget.Application.Ports;
using Bugget.Domain.DomainEvents;

namespace Bugget.Application.DomainEvents.Consumer;

/// <summary>
/// Обработчик одного типа события из локального outbox `public.domain_events`; диспатч — poller'ом по
/// <see cref="EventType"/>. Handler пишет в транзакцию обновления cursor'а: side-effect и cursor атомарны.
/// </summary>
public interface IDomainEventHandler
{
    /// <summary>Тип события, например <c>bugget.report.status_changed</c>.</summary>
    string EventType { get; }

    /// <summary>Работает в уже открытой транзакции. Исключение → poller откатит её, cursor не продвинется,
    /// событие придёт снова на следующем тике.</summary>
    Task HandleAsync(
        DomainEvent evt,
        ITransactionScope scope,
        CancellationToken ct);
}
