using System.Collections.Concurrent;
using Bugget.Application.Ports;

namespace Bugget.IntegrationTests.Fixtures;

/// <summary>
/// Записывающий адаптер сигнала раннеру: contract-тесты проверяют, что и когда
/// ушло бы наружу, не поднимая HTTP-приёмник.
/// </summary>
public sealed class RecordingBugFixRequestedNotifier : IBugFixRequestedNotifier
{
    public ConcurrentBag<BugFixRequestedPayload> Payloads { get; } = new();

    public Task NotifyAsync(BugFixRequestedPayload payload, CancellationToken cancellationToken)
    {
        Payloads.Add(payload);
        return Task.CompletedTask;
    }
}
