using Bugget.Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;

namespace Bugget.Infrastructure.Attachments;

/// <summary>
/// Композиция пережатия вложений. Всё, что знает про ImageSharp, ffmpeg и libmagic,
/// регистрируется здесь: прикладной слой видит только порты
/// <see cref="IAttachmentOptimizer"/> и <see cref="IMimeTypeDetector"/>.
/// </summary>
public static class AttachmentOptimizationExtensions
{
    public static IServiceCollection AddAttachmentOptimization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OptimizatorSettings>(configuration.GetSection(nameof(OptimizatorSettings)));

        // Настройка декодера картинок: параллелизм по числу ядер и непрерывные буферы —
        // раньше это стояло в Program, рядом с остальной композицией хоста.
        Configuration.Default.MaxDegreeOfParallelism = Environment.ProcessorCount;
        Configuration.Default.PreferContiguousImageBuffers = true;

        return services
            .AddSingleton<ImageOptimizeWriter>()
            .AddSingleton<TextOptimizeWriter>()
            .AddSingleton<VideoOptimizeWriter>()
            .AddSingleton<FfmpegService>()
            .AddHostedService<FfmpegWarmupService>()
            .AddSingleton<IAttachmentOptimizer, AttachmentOptimizer>()
            .AddSingleton<IMimeTypeDetector, MimeTypeDetector>();
    }
}
