using System.Diagnostics;
using Bugget.BO.Interfaces;
using Bugget.BO.Ports;
using Bugget.Entities.BO.AttachmentBo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugget.BO.Services.Attachments;

public sealed class VideoOptimizeWriter(
    IFileStorageClient fileStorageClient,
    IAttachmentKeyGenerator keyGen,
    FfmpegService ffmpegService,
    IOptions<OptimizatorSettings> opt,
    ILogger<VideoOptimizeWriter> logger)
{
    private static readonly SemaphoreSlim TranscodeLock = new(2, 2);
    private const int FfmpegLogLimit = 64 * 1024;

    public async Task<OptimizationResult> OptimizeWriteAsync(
        string? organizationId,
        int reportId,
        Attachment attachment,
        Stream originalStream,
        CancellationToken ct = default)
    {
        await ffmpegService.EnsureAsync(ct);

        var tempDirectory = Path.Combine(Path.GetTempPath(), "bugget-video", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        var inputExtension = Path.GetExtension(attachment.FileName);
        var inputPath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}{inputExtension}");
        var outputPath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.mp4");
        var previewPath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.webp");

        try
        {
            await WriteStreamToFileAsync(originalStream, inputPath, ct);

            // Проверяем, что файл был записан и не пустой
            if (!File.Exists(inputPath) || new FileInfo(inputPath).Length == 0)
            {
                throw new InvalidOperationException($"Input file was not created or is empty: {inputPath}");
            }

            await RunFfmpegAsync(new[]
            {
                "-y",
                "-i", inputPath,
                "-map_metadata", "-1",
                "-map", "0:v:0",
                "-map", "0:a:0?",
                "-sn",
                "-dn",
                "-vf", $"scale=min({opt.Value.VideoMaxWidth}\\,iw):-2,setsar=1",
                "-metadata:s:v:0", "rotate=0",
                "-c:v", "libx264",
                "-preset", opt.Value.VideoPreset,
                "-crf", opt.Value.VideoCrf.ToString(),
                "-pix_fmt", "yuv420p",
                "-c:a", "aac",
                "-b:a", $"{opt.Value.VideoAudioBitrateKbps}k",
                "-movflags", "+faststart",
                outputPath
            }, ct);

            await RunFfmpegAsync(new[]
            {
                "-y",
                "-i", outputPath,
                "-frames:v", "1",
                "-vf", $"scale=min({opt.Value.MaxPreviewSize}\\,iw):-2",
                "-f", "webp",
                previewPath
            }, ct);

            var storageKey = keyGen.GetOriginalKey(
                organizationId,
                reportId,
                attachment.EntityId,
                ".mp4");
            var previewKey = keyGen.GetPreviewKey(storageKey);

            await using var outputStream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var previewStream = new FileStream(previewPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await Task.WhenAll(
                fileStorageClient.WriteAsync(storageKey, outputStream, ct),
                fileStorageClient.WriteAsync(previewKey, previewStream, ct));

            return new OptimizationResult(
                FileName: Path.ChangeExtension(attachment.FileName, ".mp4"),
                StorageKey: storageKey,
                MimeType: "video/mp4",
                LengthBytes: outputStream.Length,
                IsGzipCompressed: false,
                HasPreview: true,
                PreviewLengthBytes: previewStream.Length
            );
        }
        finally
        {
            TryDeleteFile(inputPath);
            TryDeleteFile(outputPath);
            TryDeleteFile(previewPath);
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static async Task WriteStreamToFileAsync(Stream stream, string path, CancellationToken ct)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await stream.CopyToAsync(output, ct);
    }

    private async Task RunFfmpegAsync(IEnumerable<string> arguments, CancellationToken ct)
    {
        await TranscodeLock.WaitAsync(ct);
        try
        {
            var ffmpegPath = ffmpegService.GetFfmpegPath();
            if (string.IsNullOrWhiteSpace(ffmpegPath))
            {
                throw new InvalidOperationException("FFmpeg executable not found.");
            }

            var argsList = arguments.ToList();
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                RedirectStandardError = true,
                RedirectStandardOutput = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in argsList)
            {
                startInfo.ArgumentList.Add(arg);
            }

            logger.LogDebug("Running FFmpeg with arguments: {args}", string.Join(" ", argsList));

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start ffmpeg process.");
            var stderrTask = ReadLimitedAsync(process.StandardError, FfmpegLogLimit, ct);

            using var registration = ct.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to terminate ffmpeg process");
                }
            });

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to terminate ffmpeg process after cancellation");
                }

                throw;
            }

            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                var errorMessage = $"FFmpeg failed with exit code {process.ExitCode}. " +
                                 $"Command: {ffmpegPath} {string.Join(" ", argsList)}. " +
                                 $"Error output: {stderr}";
                logger.LogError("FFmpeg failed: {errorMessage}", errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
        }
        finally
        {
            TranscodeLock.Release();
        }
    }

    private async Task<string> ReadLimitedAsync(StreamReader reader, int maxChars, CancellationToken ct)
    {
        var buffer = new char[4096];
        var remaining = maxChars;
        var builder = new System.Text.StringBuilder(Math.Min(maxChars, 8192));

        var truncated = false;
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read == 0)
            {
                break;
            }

            if (remaining > 0)
            {
                var take = Math.Min(read, remaining);
                builder.Append(buffer, 0, take);
                remaining -= take;
            }
            else
            {
                truncated = true;
            }
        }

        if (truncated)
        {
            builder.Append("…");
        }

        return builder.ToString();
    }

    private void TryDeleteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete temp file {path}", path);
        }
    }

    private void TryDeleteDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete temp directory {path}", path);
        }
    }
}
