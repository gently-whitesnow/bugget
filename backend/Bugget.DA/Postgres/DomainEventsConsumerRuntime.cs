using System.Data;
using Bugget.DA.Interfaces;
using Npgsql;

namespace Bugget.DA.Postgres;

public sealed class DomainEventsConsumerRuntime(NpgsqlDataSource dataSource) : IDomainEventsConsumerRuntime
{
    public async Task RunInTransactionAsync(
        Func<IDbConnection, IDbTransaction, CancellationToken, Task> action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await action(conn, tx, ct);
        // Если action откатил транзакцию явно — Connection == null, пропускаем commit.
        if (tx.Connection is not null)
        {
            await tx.CommitAsync(ct);
        }
    }
}
