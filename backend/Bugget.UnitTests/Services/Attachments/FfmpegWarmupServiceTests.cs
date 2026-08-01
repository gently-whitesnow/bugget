using System.Reflection;
using Bugget.Application.Services.Attachments;
using Bugget.Domain.Attachments;
using Bugget.Infrastructure.Attachments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bugget.UnitTests.Services.Attachments;

public sealed class FfmpegWarmupServiceTests
{
    [Fact(DisplayName = "Выключенная видеооптимизация не запускает ffmpeg warmup")]
    public async Task Disabled_optimization_does_not_start_ffmpeg()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), $"bugget-ffmpeg-{Guid.NewGuid():N}");
        var executable = Path.Combine(directory, "ffmpeg");
        var marker = Path.Combine(directory, "started");
        Directory.CreateDirectory(directory);

        try
        {
            await File.WriteAllTextAsync(executable, $"#!/bin/sh\ntouch '{marker}'\nprintf 'fake ffmpeg\\n'\n");
            File.SetUnixFileMode(
                executable,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var options = Options.Create(new OptimizatorSettings
            {
                FfmpegDirectory = directory,
                VideoOptimizationEnabled = false,
            });
            var ffmpeg = new FfmpegService(options, NullLogger<FfmpegService>.Instance);
            using var warmup = new FfmpegWarmupService(ffmpeg, NullLogger<FfmpegWarmupService>.Instance);

            var execute = typeof(FfmpegWarmupService).GetMethod(
                "ExecuteAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            await (Task)execute.Invoke(warmup, [CancellationToken.None])!;

            File.Exists(marker).Should().BeFalse(
                "аварийный тумблер обязан исключать скачивание и запуск ffmpeg целиком");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
