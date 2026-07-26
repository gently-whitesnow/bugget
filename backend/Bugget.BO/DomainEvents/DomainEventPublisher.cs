using Bugget.DA.Interfaces;
using Bugget.DA.Transactions;
using Bugget.Entities.DbModels.DomainEvents;

namespace Bugget.BO.DomainEvents;

public sealed class DomainEventPublisher(IDomainEventsDbClient client) : IDomainEventPublisher
{
    public Task<long> PublishAsync(DomainEventDbModel evt, ITransactionScope scope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentNullException.ThrowIfNull(scope);

        return client.InsertAsync(evt, scope, ct);
    }

    public Task<long> PublishAsync(DomainEventDbModel evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        return client.InsertAsync(evt, ct);
    }
}
