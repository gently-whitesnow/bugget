using Bugget.DA.Transactions;

namespace Bugget.DA.Interfaces;

public interface IIdempotencyCacheDbClient
{
    Task<string?> TryGetAsync(string key, CancellationToken ct = default);

    Task<string?> TryGetAsync(ITransactionScope scope, string key, CancellationToken ct = default);

    Task InsertAsync(string key, string responseJson, DateTimeOffset expiresAt, CancellationToken ct = default);

    Task UpsertAsync(
        ITransactionScope scope,
        string key,
        string responseJson,
        DateTimeOffset expiresAt,
        CancellationToken ct = default);

    Task AcquireLockInternalAsync(ITransactionScope scope, string key, CancellationToken ct = default);

    Task<int> DeleteExpiredAsync(CancellationToken ct = default);
}
