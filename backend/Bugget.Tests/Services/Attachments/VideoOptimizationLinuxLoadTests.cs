using System.Diagnostics;
using System.Globalization;
using Bugget.BO.Services.Attachments;
using Bugget.Entities.BO.AttachmentBo;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bugget.Tests.Services.Attachments;

public sealed class VideoOptimizationLinuxLoadTests
{
    [Fact(DisplayName = "Linux: два 4K job держат полный cgroup ниже 800 MB")]
    public async Task Safe_profile_stays_within_container_budget()
    {
        if (Environment.GetEnvironmentVariable("BUGGET_RUN_VIDEO_LOAD") != "1")
        {
            return;
        }

        OperatingSystem.IsLinux().Should().BeTrue();
        var fixture = Environment.GetEnvironmentVariable("BUGGET_VIDEO_FIXTURE");
        File.Exists(fixture).Should().BeTrue("для load-проверки нужна реальная 4K fixture");

        var settings = new OptimizatorSettings
        {
            FfmpegDirectory = "/usr/bin",
            VideoMaxConcurrency = 1,
            VideoDecoderThreads = 1,
            VideoEncoderThreads = 1,
            VideoFilterThreads = 1,
            VideoTimeoutSeconds = 300,
            VideoPreset = "medium",
        };
        var options = Options.Create(settings);
        using var metrics = new VideoOptimizationMetrics();
        var gate = new VideoTranscodeGate(options, metrics);
        var runner = new FfmpegProcessRunner(
            new FfmpegService(options, NullLogger<FfmpegService>.Instance),
            metrics,
            NullLogger<FfmpegProcessRunner>.Instance);
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"bugget-load-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        var maxActiveFfmpeg = 0;
        using var monitorStop = new CancellationTokenSource();
        var monitor = Task.Run(async () =>
        {
            while (!monitorStop.IsCancellationRequested)
            {
                maxActiveFfmpeg = Math.Max(maxActiveFfmpeg, Process.GetProcessesByName("ffmpeg").Length);
                await Task.Delay(20, CancellationToken.None);
            }
        });

        try
        {
            await ExecuteJobsAsync(settings, gate, runner, fixture!, outputDirectory);
            maxActiveFfmpeg.Should().Be(1);
            await AssertCgroupBudgetAsync(maxActiveFfmpeg);
        }
        finally
        {
            await monitorStop.CancelAsync();
            await monitor;
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static async Task AssertCgroupBudgetAsync(int maxActiveFfmpeg)
    {
        var peakBytes = long.Parse(
            await File.ReadAllTextAsync("/sys/fs/cgroup/memory.peak"),
            CultureInfo.InvariantCulture);
        var memoryEvents = await File.ReadAllLinesAsync("/sys/fs/cgroup/memory.events");
        var oomKills = long.Parse(
            memoryEvents.Single(line => line.StartsWith("oom_kill ", StringComparison.Ordinal)).Split(' ')[1],
            CultureInfo.InvariantCulture);
        peakBytes.Should().BeLessThanOrEqualTo(800L * 1024 * 1024);
        oomKills.Should().Be(0);
        Console.WriteLine(
            $"cgroup memory.peak={peakBytes}; oom_kill={oomKills}; max active ffmpeg={maxActiveFfmpeg}");
    }

    private static async Task ExecuteJobsAsync(
        OptimizatorSettings settings,
        VideoTranscodeGate gate,
        FfmpegProcessRunner runner,
        string fixture,
        string outputDirectory)
    {
        var jobs = Enumerable.Range(0, 2).Select(async job =>
        {
            using var lease = await gate.AcquireAsync(CancellationToken.None);
            var output = Path.Combine(outputDirectory, $"output-{job}.mp4");
            var preview = Path.Combine(outputDirectory, $"preview-{job}.webp");
            await runner.RunAsync(
                VideoOptimizeWriter.BuildTranscodeArguments(settings, fixture, output),
                TimeSpan.FromMinutes(5), CancellationToken.None);
            await runner.RunAsync(
                VideoOptimizeWriter.BuildPreviewArguments(settings, output, preview),
                TimeSpan.FromMinutes(5), CancellationToken.None);
            lease.Complete(VideoOptimizeOutcome.Success);
        });

        var allJobs = Task.WhenAll(jobs);
        await WaitUntilAsync(() => Process.GetProcessesByName("ffmpeg").Length > 0);
        await using (var original = File.OpenRead(fixture))
        {
            original.Length.Should().BeGreaterThan(0, "оригинал доступен во время фоновой оптимизации");
        }

        await allJobs;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 500 && !condition(); attempt++)
        {
            await Task.Delay(20);
        }

        condition().Should().BeTrue("ffmpeg обязан стартовать в течение 10 секунд");
    }
}
