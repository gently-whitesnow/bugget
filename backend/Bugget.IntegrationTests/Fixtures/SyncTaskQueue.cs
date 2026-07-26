using Microsoft.Extensions.DependencyInjection;
using TaskQueue;

namespace Bugget.IntegrationTests.Fixtures;

/// <summary>
/// Тестовый ITaskQueue, выполняющий work item'ы синхронно. Реальный TaskQueue —
/// BackgroundService, который AppWithPostgresFixture снимает через RemoveAll&lt;IHostedService&gt;().
/// Без этого фейка SignalR-push, эмитимый через taskQueue.EnqueueAsync (например,
/// AttachmentService.SaveAsync), не сработает в тестах.
/// </summary>
public sealed class SyncTaskQueue(IServiceProvider serviceProvider) : ITaskQueue
{
    public async ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem)
    {
        using var scope = serviceProvider.CreateScope();
        try
        {
            await workItem(scope.ServiceProvider, CancellationToken.None);
        }
        catch
        {
            // Симметрично реальному TaskQueue, который ловит и логирует, чтобы фоновая
            // ошибка не валила запрос. Для тестов SignalR-push успевает зарегистрироваться
            // через Task.WhenAll до возможного падения в downstream-шагах (например, optimizator).
        }
    }

    public ValueTask EnqueueAsync(Func<CancellationToken, Task> workItem)
        => EnqueueAsync((_, ct) => workItem(ct));

    public ValueTask EnqueueAsync(Func<Task> workItem)
        => EnqueueAsync((_, _) => workItem());
}
