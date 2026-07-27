using Bugget.DA.Transactions;
using Bugget.Entities.DbModels.DomainEvents;

namespace Bugget.DA.Interfaces;

public interface IDomainEventsDbClient
{
    Task<long> InsertAsync(DomainEventDbModel evt, ITransactionScope scope, CancellationToken ct = default);

    /// <summary>
    /// INSERT через собственное соединение (без транзакции). Используется
    /// publisher'ом для аудит-событий, не требующих атомарности с доменным
    /// UPDATE. См. <see cref="Bugget.BO.DomainEvents.IDomainEventPublisher"/>.
    /// </summary>
    Task<long> InsertAsync(DomainEventDbModel evt, CancellationToken ct = default);

    /// <summary>
    /// Глобальная (без фильтра по <c>workspace_id</c>) выборка хвоста событий для
    /// локального outbox-консьюмера: проекция глобальная, per-workspace не нужно.
    /// </summary>
    Task<IReadOnlyList<DomainEventDbModel>> ListAllAsync(
        long sinceId,
        int limit,
        CancellationToken ct = default);

    /// <summary>
    /// Глобальный <c>MAX(id) FROM domain_events</c>. Используется при bootstrap'е
    /// <c>domain_events_cursor</c>, чтобы новый консьюмер не пропахивал исторический хвост.
    /// </summary>
    Task<long> GetLatestIdAcrossAllAsync(CancellationToken ct = default);
}
