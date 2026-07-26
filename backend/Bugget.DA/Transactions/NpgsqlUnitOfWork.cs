using Npgsql;

namespace Bugget.DA.Transactions;

public sealed class NpgsqlUnitOfWork(NpgsqlDataSource dataSource) : IUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(
        Func<ITransactionScope, CancellationToken, Task<T>> action,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        var scope = new NpgsqlTransactionScope(connection, tx);

        var result = await action(scope, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task ExecuteAsync(
        Func<ITransactionScope, CancellationToken, Task> action,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        var scope = new NpgsqlTransactionScope(connection, tx);

        await action(scope, ct);
        await tx.CommitAsync(ct);
    }
}
