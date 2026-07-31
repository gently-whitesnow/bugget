using Bugget.BO.Services.Attachments;
using FluentAssertions;

namespace Bugget.Tests.Services.Attachments;

/// <summary>
/// Диагностика падения ffmpeg обязана остаться читаемой, но не должна выносить наружу
/// временные пути и имя файла пользователя: и лог, и текст исключения фоновой задачи
/// уезжают в общий журнал (MAIN-240).
/// </summary>
public sealed class FfmpegStderrSanitizerTests
{
    private static readonly string[] Arguments = ["-i", "/tmp/bugget-video/ab12/capture.MOV", "/tmp/bugget-video/ab12/out.mp4"];

    [Fact(DisplayName = "Причина отказа остаётся, путь и имя файла — нет")]
    public void Reason_survives_without_paths()
    {
        var summary = FfmpegStderrSanitizer.Summarize(
            "frame= 12 fps=3\n[in#0 @ 0x7f9a1c] /tmp/bugget-video/ab12/capture.MOV: Invalid data found when processing input\n",
            Arguments);

        summary.Should().Contain("Invalid data found when processing input");
        summary.Should().NotContain("/tmp");
        summary.Should().NotContain("capture");
        summary.Should().NotContain("0x7f9a1c");
    }

    [Fact(DisplayName = "Голое имя файла в чужом формате тоже вырезается")]
    public void Bare_file_name_is_redacted()
    {
        var summary = FfmpegStderrSanitizer.Summarize("secret-report.mov: No such file or directory", Arguments);

        summary.Should().NotContain("secret-report");
        summary.Should().Contain("No such file or directory");
    }

    [Fact(DisplayName = "Пустой stderr даёт низкокардинальную заглушку")]
    public void Empty_stderr_has_stable_summary()
    {
        FfmpegStderrSanitizer.Summarize("   \n", Arguments).Should().Be("no stderr");
    }

    [Fact(DisplayName = "Длинный stderr обрезается и не раздувает лог")]
    public void Long_stderr_is_truncated()
    {
        var summary = FfmpegStderrSanitizer.Summarize(new string('a', 5000), Arguments);

        summary.Length.Should().BeLessThan(400);
    }
}
