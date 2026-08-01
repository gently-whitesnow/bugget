using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Bugget.Infrastructure.Attachments;

public sealed class FfmpegService(IOptions<OptimizatorSettings> options, ILogger<FfmpegService> logger)
{
    private readonly SemaphoreSlim _ffmpegLock = new(1, 1);
    private bool _ffmpegReady;

    /// <summary>Первая строка <c>ffmpeg -version</c>: без неё непонятно, чей профиль потоков мы видим в логах.</summary>
    public string? Version { get; private set; }

    public async Task EnsureAsync(CancellationToken ct)
    {
        // Аварийный тумблер выключает ffmpeg целиком: ни скачивания, ни стартового
        // `ffmpeg -version`. Установка без памяти под ffmpeg не должна его даже трогать
        // (MAIN-240). Перекодирования при выключенном тумблере всё равно нет —
        // VideoOptimizeWriter кладёт оригинал под постоянный ключ.
        if (_ffmpegReady || !options.Value.VideoOptimizationEnabled)
        {
            return;
        }

        await _ffmpegLock.WaitAsync(ct);
        try
        {
            if (_ffmpegReady)
            {
                return;
            }

            var ffmpegDirectory = ResolveFfmpegDirectory();
            Directory.CreateDirectory(ffmpegDirectory);

            var ffmpegPath = GetExecutablePath(ffmpegDirectory);
            if (!File.Exists(ffmpegPath))
            {
                logger.LogInformation("Downloading FFmpeg to {path}", ffmpegDirectory);
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ffmpegDirectory);
            }

            FFmpeg.SetExecutablesPath(ffmpegDirectory);
            _ffmpegReady = true;
            Version = await ReadVersionAsync(ffmpegPath, ct);
            logger.LogInformation("FFmpeg ready at {path}, version {version}", ffmpegPath, Version ?? "unknown");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize FFmpeg");
            throw;
        }
        finally
        {
            _ffmpegLock.Release();
        }
    }

    public string? GetFfmpegPath()
    {
        if (!string.IsNullOrWhiteSpace(FFmpeg.ExecutablesPath))
        {
            var configured = GetExecutablePath(FFmpeg.ExecutablesPath);
            if (File.Exists(configured))
            {
                return configured;
            }
        }

        var directory = ResolveFfmpegDirectory();
        var path = GetExecutablePath(directory);
        return File.Exists(path) ? path : null;
    }

    private async Task<string?> ReadVersionAsync(string ffmpegPath, CancellationToken ct)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = ffmpegPath,
                ArgumentList = { "-version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null)
            {
                return null;
            }

            var firstLine = await process.StandardOutput.ReadLineAsync(ct);
            await process.WaitForExitAsync(ct);
            return firstLine?.Trim();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read FFmpeg version");
            return null;
        }
    }

    private string ResolveFfmpegDirectory()
    {
        var configured = options.Value.FfmpegDirectory;
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("FfmpegDirectory must be configured.");
        }

        return Path.GetFullPath(configured);
    }

    private static string GetExecutablePath(string directory)
    {
        return Path.Combine(
            directory,
            OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
    }
}
