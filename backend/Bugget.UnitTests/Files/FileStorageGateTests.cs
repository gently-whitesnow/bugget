using Bugget.Infrastructure.Files;
using FluentAssertions;
using Xunit;

namespace Bugget.UnitTests.Files;

public class FileStorageGateTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(200);

    [Fact(DisplayName = "Зависшая операция отдаёт TimeoutException по таймауту")]
    public async Task HangingOperationTimesOut()
    {
        using var hang = new ManualResetEventSlim();
        var gate = new FileStorageGate(maxConcurrentOperations: 1, Timeout);

        var act = () => gate.RunAsync(() => hang.Wait());

        await act.Should().ThrowAsync<TimeoutException>();
        hang.Set();
    }

    [Fact(DisplayName = "Пока зависшие операции держат все слоты, новая не запускается и падает по таймауту")]
    public async Task StuckOperationsKeepSlots()
    {
        using var hang = new ManualResetEventSlim();
        var gate = new FileStorageGate(maxConcurrentOperations: 1, Timeout);
        await FluentActions.Awaiting(() => gate.RunAsync(() => hang.Wait())).Should().ThrowAsync<TimeoutException>();

        var started = false;
        var act = () => gate.RunAsync(() => started = true);

        await act.Should().ThrowAsync<TimeoutException>();
        started.Should().BeFalse();
        hang.Set();
    }

    [Fact(DisplayName = "Слот освобождается, когда зависшая операция всё-таки завершилась")]
    public async Task SlotReleasedAfterStuckOperationCompletes()
    {
        using var hang = new ManualResetEventSlim();
        var gate = new FileStorageGate(maxConcurrentOperations: 1, Timeout);
        await FluentActions.Awaiting(() => gate.RunAsync(() => hang.Wait())).Should().ThrowAsync<TimeoutException>();

        hang.Set();

        (await gate.RunAsync(() => 42)).Should().Be(42);
    }

    [Fact(DisplayName = "Результат брошенной по таймауту операции освобождается после её завершения")]
    public async Task AbandonedResultIsDisposed()
    {
        using var hang = new ManualResetEventSlim();
        var resource = new TrackingDisposable();
        var gate = new FileStorageGate(maxConcurrentOperations: 1, Timeout);
        await FluentActions.Awaiting(() => gate.RunAsync(() =>
        {
            hang.Wait();
            return resource;
        })).Should().ThrowAsync<TimeoutException>();

        hang.Set();

        await resource.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "Ошибка операции доходит до вызывающего как есть")]
    public async Task OperationErrorPropagates()
    {
        var gate = new FileStorageGate(maxConcurrentOperations: 1, Timeout);

        var act = () => gate.RunAsync(() => throw new FileNotFoundException("missing"));

        await act.Should().ThrowAsync<FileNotFoundException>();
        (await gate.RunAsync(() => 1)).Should().Be(1);
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Dispose() => Disposed.TrySetResult();
    }
}
