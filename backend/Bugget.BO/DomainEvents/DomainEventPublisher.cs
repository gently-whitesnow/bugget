using Bugget.BO.Ports;
using Bugget.Entities.BO.DomainEvents;

namespace Bugget.BO.DomainEvents;

public sealed class DomainEventPublisher(IDomainEventsDbClient client) : IDomainEventPublisher
{
    public Task<long> PublishAsync(DomainEvent evt, ITransactionScope scope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentNullException.ThrowIfNull(scope);

        return client.InsertAsync(evt, scope, ct);
    }

    public Task<long> PublishAsync(DomainEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        return client.InsertAsync(evt, ct);
    }
}
