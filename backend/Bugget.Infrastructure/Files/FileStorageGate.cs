namespace Bugget.Infrastructure.Files;

// Зависший системный вызов не отменить: слот держится до его завершения, а не до таймаута.
public sealed class FileStorageGate
{
    private readonly SemaphoreSlim _slots;
    private readonly int _maxConcurrentOperations;
    private readonly TimeSpan _timeout;

    public FileStorageGate(int maxConcurrentOperations, TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentOperations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        _maxConcurrentOperations = maxConcurrentOperations;
        _timeout = timeout;
        _slots = new SemaphoreSlim(maxConcurrentOperations, maxConcurrentOperations);
    }

    /// <summary>Результат брошенной по таймауту операции освобождается, когда она всё-таки завершится.</summary>
    public async Task<T> RunAsync<T>(Func<T> operation, CancellationToken ct = default)
    {
        if (!await _slots.WaitAsync(_timeout, ct))
        {
            throw new TimeoutException(
                $"File storage did not free a slot within {_timeout}: all {_maxConcurrentOperations} operations are in progress.");
        }

        var task = Task.Run(operation, CancellationToken.None);
        _ = task.ContinueWith(
            static (completed, slots) =>
            {
                _ = completed.Exception;
                ((SemaphoreSlim)slots!).Release();
            },
            _slots,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            return await task.WaitAsync(_timeout, ct);
        }
        catch (Exception e) when (e is TimeoutException or OperationCanceledException && !task.IsCompleted)
        {
            _ = task.ContinueWith(
                static completed => (completed.Result as IDisposable)?.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            if (e is TimeoutException)
            {
                throw new TimeoutException($"File storage operation did not complete within {_timeout}.", e);
            }

            throw;
        }
    }

    public Task RunAsync(Action operation, CancellationToken ct = default) =>
        RunAsync(() =>
        {
            operation();
            return true;
        }, ct);
}
