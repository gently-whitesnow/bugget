using Bugget.DA.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bugget.HostedServices;

public sealed class IdempotencyCacheCleanupService(
    IIdempotencyCacheDbClient db,
    ILogger<IdempotencyCacheCleanupService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                var deleted = await db.DeleteExpiredAsync(stoppingToken);
                if (deleted > 0)
                {
                    logger.LogInformation("Idempotency cache cleanup removed {Count} expired entries.", deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Idempotency cache cleanup failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
