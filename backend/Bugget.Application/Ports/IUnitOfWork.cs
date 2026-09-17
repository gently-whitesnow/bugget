namespace Bugget.Application.Ports;

/// <summary>
/// Граница транзакции для BO-сервисов. BO выражает «эта последовательность шагов
/// атомарна», не открывая соединение/транзакцию напрямую.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Выполняет <paramref name="action"/> в транзакции и коммитит; при исключении — откат через Dispose.</summary>
    Task<T> ExecuteAsync<T>(
        Func<ITransactionScope, CancellationToken, Task<T>> action,
        CancellationToken ct = default);

    Task ExecuteAsync(
        Func<ITransactionScope, CancellationToken, Task> action,
        CancellationToken ct = default);
}
