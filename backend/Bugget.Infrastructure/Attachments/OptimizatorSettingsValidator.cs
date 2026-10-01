using Microsoft.Extensions.Options;

namespace Bugget.Infrastructure.Attachments;

/// <summary>
/// Проверка профиля оптимизации на старте (<c>ValidateOnStart</c>), чтобы установка не уехала молча в OOM (MAIN-188).
/// Настройки из external_settings.json правит человек, поэтому отказ громкий и с причиной.
/// </summary>
public sealed class OptimizatorSettingsValidator : IValidateOptions<OptimizatorSettings>
{
    /// <summary>Пресеты x264: чужое значение ffmpeg отвергает уже в рантайме фоновой задачи.</summary>
    private static readonly string[] KnownVideoPresets =
    [
        "ultrafast", "superfast", "veryfast", "faster", "fast",
        "medium", "slow", "slower", "veryslow", "placebo"
    ];

    /// <summary>Во сколько раз бюджет потоков ffmpeg может превышать число ядер: запас под обычный oversubscribe.</summary>
    private const int ThreadBudgetOversubscribeFactor = 2;

    /// <summary>Нижняя граница бюджета: безопасный профиль обязан подниматься даже на одноядерной машине.</summary>
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

        // Считаем в long: произведение int-ов из настроек переполняется и проходит проверку бюджета (MAIN-240).
        var threadsPerJob = (long)options.VideoDecoderThreads + options.VideoEncoderThreads + options.VideoFilterThreads;
        var threadBudget = MultiplySaturating(options.VideoMaxConcurrency, threadsPerJob);
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

    /// <summary>Насыщающее умножение: переполнение обязано выглядеть как «бюджет исчерпан».</summary>
    private static long MultiplySaturating(long left, long right)
    {
        try
        {
            return checked(left * right);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    private static void RequirePositive(List<string> failures, int value, string name)
    {
        if (value <= 0)
        {
            failures.Add($"{name} должен быть больше нуля, а не {value}.");
        }
    }
}
