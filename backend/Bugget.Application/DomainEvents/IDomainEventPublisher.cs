using Bugget.Application.Ports;
using Bugget.Domain.DomainEvents;

namespace Bugget.Application.DomainEvents;

public interface IDomainEventPublisher
{
    Task<long> PublishAsync(DomainEvent evt, ITransactionScope scope, CancellationToken ct = default);

    /// <summary>
    /// Публикация события без явной транзакции — INSERT через собственное соединение.
    /// Используется для аудит-событий, которые не должны делить атомарность с
    /// доменным UPDATE (см. ReportExcludedFromAnalyticsToggled: projection его
    /// игнорирует, потеря одной строки аудита приемлема). Не использовать для
    /// событий, на которых строится read-model.
    /// </summary>
    Task<long> PublishAsync(DomainEvent evt, CancellationToken ct = default);
}
