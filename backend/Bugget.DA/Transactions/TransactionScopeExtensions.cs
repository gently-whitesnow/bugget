using Npgsql;

namespace Bugget.DA.Transactions;

/// <summary>
/// Внутренний helper для DA-клиентов: извлекает Npgsql-объекты из <see cref="ITransactionScope"/>.
/// BO-слой не должен использовать этот класс — он internal.
/// </summary>
internal static class TransactionScopeExtensions
{
    public static (NpgsqlConnection connection, NpgsqlTransaction transaction) Unwrap(this ITransactionScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope is not NpgsqlTransactionScope npgsql)
        {
            throw new InvalidOperationException(
                $"Unsupported {nameof(ITransactionScope)} implementation: {scope.GetType().FullName}.");
        }
        return (npgsql.Connection, npgsql.Transaction);
    }
}
