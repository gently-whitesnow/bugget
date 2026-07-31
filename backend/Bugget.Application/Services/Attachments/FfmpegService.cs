using Bugget.Domain.Attachments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Bugget.Application.Services.Attachments;

public sealed class FfmpegService(IOptions<OptimizatorSettings> options, ILogger<FfmpegService> logger)
{
    private readonly SemaphoreSlim _ffmpegLock = new(1, 1);
    private bool _ffmpegReady;

    public async Task EnsureAsync(CancellationToken ct)
    {
        if (_ffmpegReady)
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
            if (File.Exists(ffmpegPath))
            {
                FFmpeg.SetExecutablesPath(ffmpegDirectory);
                _ffmpegReady = true;
                logger.LogInformation("Using existing FFmpeg at {path}", ffmpegPath);
                return;
            }

            logger.LogInformation("Downloading FFmpeg to {path}", ffmpegDirectory);
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ffmpegDirectory);
            FFmpeg.SetExecutablesPath(ffmpegDirectory);
            _ffmpegReady = true;
            logger.LogInformation("FFmpeg download completed");
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
