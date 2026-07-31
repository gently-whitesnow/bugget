using Bugget.Entities.BO.AttachmentBo;
using Microsoft.Extensions.Options;

namespace Bugget.BO.Services.Attachments;

/// <summary>
/// Проверка профиля оптимизации на старте (<c>ValidateOnStart</c>). Смысл — не дать
/// установке молча уехать в OOM: нулевой или отрицательный потолок раньше означал
/// «без ограничений», а перемноженные потоки — несколько тяжёлых ffmpeg-ов сразу
/// (MAIN-188). Настройки приходят из external_settings.json, править их будет человек,
/// поэтому отказ должен быть громким и с причиной.
/// </summary>
public sealed class OptimizatorSettingsValidator : IValidateOptions<OptimizatorSettings>
{
    /// <summary>Пресеты x264: чужое значение ffmpeg отвергает уже в рантайме фоновой задачи.</summary>
    private static readonly string[] KnownVideoPresets =
    [
        "ultrafast", "superfast", "veryfast", "faster", "fast",
        "medium", "slow", "slower", "veryslow", "placebo"
    ];

    /// <summary>
    /// Во сколько раз суммарный бюджет потоков ffmpeg может превышать число ядер.
    /// Двукратный запас оставляет место обычному oversubscribe и режет заведомо
    /// невыполнимые сочетания вроде «4 процесса по 8 потоков» на двух ядрах.
    /// </summary>
    private const int ThreadBudgetOversubscribeFactor = 2;

    /// <summary>
    /// Нижняя граница бюджета: безопасный профиль — по одному потоку на декодер,
    /// кодировщик и фильтры — обязан подниматься даже на одноядерной машине.
    /// </summary>
    private const int MinThreadBudget = 3;

    public ValidateOptionsResult Validate(string? name, OptimizatorSettings options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.FfmpegDirectory))
        {
            failures.Add($"{nameof(options.FfmpegDirectory)} обязателен.");
        }

        RequirePositive(failures, options.VideoMaxConcurrency, nameof(options.VideoMaxConcurrency));
        RequirePositive(failures, options.VideoEncoderThreads, nameof(options.VideoEncoderThreads));
        RequirePositive(failures, options.VideoDecoderThreads, nameof(options.VideoDecoderThreads));
        RequirePositive(failures, options.VideoFilterThreads, nameof(options.VideoFilterThreads));
        RequirePositive(failures, options.VideoTimeoutSeconds, nameof(options.VideoTimeoutSeconds));
        RequirePositive(failures, options.VideoMaxWidth, nameof(options.VideoMaxWidth));
        RequirePositive(failures, options.VideoAudioBitrateKbps, nameof(options.VideoAudioBitrateKbps));

        if (options.VideoCrf is < 0 or > 51)
        {
            failures.Add($"{nameof(options.VideoCrf)} должен быть в диапазоне 0..51, а не {options.VideoCrf}.");
        }

        if (!KnownVideoPresets.Contains(options.VideoPreset, StringComparer.Ordinal))
        {
            failures.Add(
                $"{nameof(options.VideoPreset)}=\"{options.VideoPreset}\" не входит в пресеты x264: " +
                string.Join(", ", KnownVideoPresets) + ".");
        }

        var threadsPerJob = options.VideoDecoderThreads + options.VideoEncoderThreads + options.VideoFilterThreads;
        var threadBudget = options.VideoMaxConcurrency * threadsPerJob;
        var allowedThreads = Math.Max(
            MinThreadBudget,
            ThreadBudgetOversubscribeFactor * Environment.ProcessorCount);
        if (threadBudget > allowedThreads)
        {
            failures.Add(
                $"{nameof(options.VideoMaxConcurrency)} x ({nameof(options.VideoDecoderThreads)} + " +
                $"{nameof(options.VideoEncoderThreads)} + {nameof(options.VideoFilterThreads)}) = {threadBudget} " +
                $"превышает бюджет потоков {allowedThreads} на {Environment.ProcessorCount} ядрах.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void RequirePositive(List<string> failures, int value, string name)
    {
        if (value <= 0)
        {
            failures.Add($"{name} должен быть больше нуля, а не {value}.");
        }
    }
}
