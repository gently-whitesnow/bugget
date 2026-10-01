using Bugget.Domain.DomainEvents;

namespace Bugget.Application.Ports;

public interface IDomainEventsDbClient
{
    Task<long> InsertAsync(DomainEvent evt, ITransactionScope scope, CancellationToken ct = default);

    /// <summary>
    /// INSERT через собственное соединение, без транзакции: для аудит-событий, не требующих атомарности с доменным
    /// UPDATE. См. <see cref="Bugget.Application.DomainEvents.IDomainEventPublisher"/>.
    /// </summary>
    Task<long> InsertAsync(DomainEvent evt, CancellationToken ct = default);

    /// <summary>Хвост событий без фильтра по <c>workspace_id</c>: проекция outbox-консьюмера глобальная.</summary>
    Task<IReadOnlyList<DomainEvent>> ListAllAsync(
        long sinceId,
        int limit,
        CancellationToken ct = default);

    /// <summary>Глобальный <c>MAX(id)</c>: при bootstrap'е курсора новый консьюмер не пропахивает исторический хвост.</summary>
    Task<long> GetLatestIdAcrossAllAsync(CancellationToken ct = default);
}
