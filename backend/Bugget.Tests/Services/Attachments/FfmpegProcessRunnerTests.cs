using System.Diagnostics;
using Bugget.BO.Services.Attachments;
using Bugget.Entities.BO.AttachmentBo;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bugget.Tests.Services.Attachments;

/// <summary>
/// Таймаут обязан убивать всё дерево процессов, а не только прямого ребёнка: ffmpeg
/// оставленный жить, продолжает есть память уже без надзора (MAIN-188). Проверяется на
/// /bin/sh с внуком — настоящий ffmpeg для этого не нужен и в юнит-прогоне его нет.
/// </summary>
public sealed class FfmpegProcessRunnerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    private static FfmpegProcessRunner Runner()
    {
        var options = Options.Create(new OptimizatorSettings());
        return new FfmpegProcessRunner(
            new FfmpegService(options, NullLogger<FfmpegService>.Instance),
            new VideoOptimizationMetrics(),
            NullLogger<FfmpegProcessRunner>.Instance);
    }

    [Fact(DisplayName = "Таймаут завершает дерево процессов")]
    public async Task Timeout_kills_the_whole_process_tree()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var pidFile = Path.Combine(Path.GetTempPath(), $"bugget-runner-{Guid.NewGuid():N}.pid");
        var script = $"sleep 120 & echo $! > {pidFile}; wait";
        var started = Stopwatch.StartNew();

        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() =>
                Runner().RunProcessAsync("/bin/sh", ["-c", script], Timeout, CancellationToken.None));

            started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));

            var grandchildPid = int.Parse(File.ReadAllText(pidFile).Trim());
            await WaitUntilGoneAsync(grandchildPid);
            IsAlive(grandchildPid).Should().BeFalse("внук обязан умереть вместе с деревом");
        }
        finally
        {
            File.Delete(pidFile);
        }
    }

    [Fact(DisplayName = "Внешняя отмена не выдаётся за таймаут")]
    public async Task External_cancellation_is_not_reported_as_timeout()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Runner().RunProcessAsync("/bin/sh", ["-c", "sleep 120"], TimeSpan.FromMinutes(5), cts.Token));
    }

    [Fact(DisplayName = "Ненулевой код возврата поднимается ошибкой")]
    public async Task Non_zero_exit_code_fails()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runner().RunProcessAsync("/bin/sh", ["-c", "exit 3"], Timeout, CancellationToken.None));
    }

    private static async Task WaitUntilGoneAsync(int pid)
    {
        for (var attempt = 0; attempt < 20 && IsAlive(pid); attempt++)
        {
            await Task.Delay(100);
        }
    }

    private static bool IsAlive(int pid)
    {
        try
        {
            return !Process.GetProcessById(pid).HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
