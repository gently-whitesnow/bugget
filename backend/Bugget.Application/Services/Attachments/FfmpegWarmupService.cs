using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bugget.Application.Services.Attachments;

public sealed class FfmpegWarmupService(FfmpegService ffmpegService, ILogger<FfmpegWarmupService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ffmpegService.EnsureAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FFmpeg warmup failed");
        }
    }
}
