namespace Bugget.Application.Ports;

public interface ITaskQueue
{

    ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem);


    ValueTask EnqueueAsync(Func<CancellationToken, Task> workItem);


    ValueTask EnqueueAsync(Func<Task> workItem);
}
