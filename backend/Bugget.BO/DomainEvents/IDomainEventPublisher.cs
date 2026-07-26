using Bugget.DA.Transactions;
using Bugget.Entities.DbModels.DomainEvents;

namespace Bugget.BO.DomainEvents;

public interface IDomainEventPublisher
{
    Task<long> PublishAsync(DomainEventDbModel evt, ITransactionScope scope, CancellationToken ct = default);

    /// <summary>
    /// Публикация события без явной транзакции — INSERT через собственное соединение.
    /// Используется для аудит-событий, которые не должны делить атомарность с
    /// доменным UPDATE (см. ReportExcludedFromAnalyticsToggled: projection его
    /// игнорирует, потеря одной строки аудита приемлема). Не использовать для
    /// событий, на которых строится read-model.
    /// </summary>
    Task<long> PublishAsync(DomainEventDbModel evt, CancellationToken ct = default);
}
