using System.Data;

namespace Bugget.DA.Interfaces;

/// <summary>
/// DA-обёртка над per-event транзакцией для outbox-консьюмера: инкапсулирует
/// Npgsql, чтобы BO-слой работал только с <see cref="IDbConnection"/> /
/// <see cref="IDbTransaction"/> (см. architecture-guard
/// <c>BoLayer_DoesNotImportNpgsql</c>).
/// </summary>
public interface IDomainEventsConsumerRuntime
{
    /// <summary>
    /// Открывает соединение и транзакцию, отдаёт их callback'у, при отсутствии
    /// исключения коммитит; исключение → rollback (через Dispose).
    /// </summary>
    Task RunInTransactionAsync(
        Func<IDbConnection, IDbTransaction, CancellationToken, Task> action,
        CancellationToken ct);
}
