using Bugget.BO.Ports;
using Npgsql;

namespace Bugget.DA.Transactions;

/// <summary>
/// Реализация <see cref="ITransactionScope"/> поверх Npgsql. Видна только DA-проекту.
/// </summary>
internal sealed class NpgsqlTransactionScope(NpgsqlConnection connection, NpgsqlTransaction transaction) : ITransactionScope
{
    public NpgsqlConnection Connection { get; } = connection;
    public NpgsqlTransaction Transaction { get; } = transaction;
}
