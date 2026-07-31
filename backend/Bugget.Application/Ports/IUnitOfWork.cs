namespace Bugget.Application.Ports;

/// <summary>
/// Граница транзакции для BO-сервисов. BO выражает «эта последовательность шагов
/// атомарна», не открывая соединение/транзакцию напрямую.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Открывает соединение и транзакцию, выполняет <paramref name="action"/>
    /// и коммитит. При исключении транзакция откатывается (через Dispose).
    /// </summary>
    Task<T> ExecuteAsync<T>(
        Func<ITransactionScope, CancellationToken, Task<T>> action,
        CancellationToken ct = default);

    /// <summary>
    /// Не-возвращающий вариант <see cref="ExecuteAsync{T}"/>.
    /// </summary>
    Task ExecuteAsync(
        Func<ITransactionScope, CancellationToken, Task> action,
        CancellationToken ct = default);
}
