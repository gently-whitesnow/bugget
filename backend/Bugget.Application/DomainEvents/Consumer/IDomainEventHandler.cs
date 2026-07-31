using Bugget.Application.Ports;
using Bugget.Domain.DomainEvents;

namespace Bugget.Application.DomainEvents.Consumer;

/// <summary>
/// Обработчик одного типа domain event из локального outbox `public.domain_events`.
/// Регистрация — DI как <c>IEnumerable&lt;IDomainEventHandler&gt;</c>, диспатч —
/// poller'ом по <see cref="EventType"/>. Handler пишет в ту же транзакцию, что
/// и обновление cursor'а: side-effect и cursor атомарны.
/// </summary>
public interface IDomainEventHandler
{
    /// <summary>Тип события, например <c>bugget.report.status_changed</c>.</summary>
    string EventType { get; }

    /// <summary>
    /// Обработать событие в рамках уже открытой транзакции. Исключение → poller
    /// откатит транзакцию, cursor не продвинется, событие переедет на следующем тике.
    /// </summary>
    Task HandleAsync(
        DomainEvent evt,
        ITransactionScope scope,
        CancellationToken ct);
}
