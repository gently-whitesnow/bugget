using Bugget.BO.Services.Attachments;
using Bugget.Entities.BO.AttachmentBo;
using FluentAssertions;

namespace Bugget.Tests.Services.Attachments;

/// <summary>
/// Профиль оптимизации приходит из external_settings.json и правится руками. Молчаливо
/// принятый ноль или невыполнимый бюджет потоков раньше означал бы «сколько угодно
/// ffmpeg-ов» — ровно тот сценарий, из-за которого bugget-api уходил в OOM (MAIN-188).
/// </summary>
public sealed class OptimizatorSettingsValidatorTests
{
    private readonly OptimizatorSettingsValidator _validator = new();

    [Fact(DisplayName = "Умолчания приложения проходят валидацию")]
    public void Defaults_are_valid()
    {
        _validator.Validate(null, new OptimizatorSettings()).Succeeded.Should().BeTrue();
    }

    [Theory(DisplayName = "Нулевые и отрицательные потолки отвергаются")]
    [InlineData(0, 1, 1, 1, 900)]
    [InlineData(-1, 1, 1, 1, 900)]
    [InlineData(1, 0, 1, 1, 900)]
    [InlineData(1, 1, 0, 1, 900)]
    [InlineData(1, 1, 1, 0, 900)]
    [InlineData(1, 1, 1, 1, 0)]
    [InlineData(1, 1, 1, 1, -30)]
    public void Non_positive_limits_are_rejected(
        int concurrency,
        int encoderThreads,
        int decoderThreads,
        int filterThreads,
        int timeout)
    {
        var result = _validator.Validate(null, new OptimizatorSettings
        {
            VideoMaxConcurrency = concurrency,
            VideoEncoderThreads = encoderThreads,
            VideoDecoderThreads = decoderThreads,
            VideoFilterThreads = filterThreads,
            VideoTimeoutSeconds = timeout,
        });

        result.Failed.Should().BeTrue();
    }

    [Fact(DisplayName = "Бюджет потоков сверх числа ядер отвергается")]
    public void Thread_budget_beyond_cpu_count_is_rejected()
    {
        var beyondAnyMachine = Environment.ProcessorCount * 4;

        var result = _validator.Validate(null, new OptimizatorSettings
        {
            VideoMaxConcurrency = beyondAnyMachine,
            VideoEncoderThreads = 8,
            VideoDecoderThreads = 8,
            VideoFilterThreads = 8,
        });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(OptimizatorSettings.VideoMaxConcurrency));
    }

    [Fact(DisplayName = "Максимальные значения не обходят бюджет переполнением int")]
    public void Maximum_values_cannot_bypass_thread_budget()
    {
        var result = _validator.Validate(null, new OptimizatorSettings
        {
            VideoMaxConcurrency = int.MaxValue,
            VideoEncoderThreads = int.MaxValue,
            VideoDecoderThreads = int.MaxValue,
            VideoFilterThreads = int.MaxValue,
        });

        result.Failed.Should().BeTrue(
            "конфиг из внешнего JSON не должен превращать опасный бюджет в малое число при переполнении");
        result.FailureMessage.Should().Contain(nameof(OptimizatorSettings.VideoMaxConcurrency));
    }

    [Fact(DisplayName = "Неизвестный пресет x264 отвергается")]
    public void Unknown_preset_is_rejected()
    {
        var result = _validator.Validate(null, new OptimizatorSettings { VideoPreset = "turbo" });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(OptimizatorSettings.VideoPreset));
    }

    [Fact(DisplayName = "CRF вне 0..51 отвергается")]
    public void Crf_out_of_range_is_rejected()
    {
        _validator.Validate(null, new OptimizatorSettings { VideoCrf = 52 }).Failed.Should().BeTrue();
    }

    [Fact(DisplayName = "Пустой каталог ffmpeg отвергается")]
    public void Empty_ffmpeg_directory_is_rejected()
    {
        _validator.Validate(null, new OptimizatorSettings { FfmpegDirectory = "  " }).Failed.Should().BeTrue();
    }
}
