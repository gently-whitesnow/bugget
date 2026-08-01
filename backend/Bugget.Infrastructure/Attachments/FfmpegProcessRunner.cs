using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Bugget.Infrastructure.Attachments;

/// <summary>
/// Запуск ffmpeg как дочернего процесса под таймаутом. Здесь же живут две вещи,
/// без которых потолок памяти не держится: убийство всего дерева процессов по
/// таймауту или отмене и замер пикового RSS ребёнка (MAIN-188).
/// </summary>
public sealed class FfmpegProcessRunner(
    FfmpegService ffmpegService,
    VideoOptimizationMetrics metrics,
    ILogger<FfmpegProcessRunner> logger)
{
    private const int StderrLogLimit = 64 * 1024;
    private static readonly TimeSpan RssSampleInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>Запускает ffmpeg. Кидает <see cref="TimeoutException"/>, если истёк <paramref name="timeout"/>.</summary>
    public async Task RunAsync(IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct)
    {
        await ffmpegService.EnsureAsync(ct);

        var ffmpegPath = ffmpegService.GetFfmpegPath();
        if (string.IsNullOrWhiteSpace(ffmpegPath))
        {
            throw new InvalidOperationException("FFmpeg executable not found.");
        }

        await RunProcessAsync(ffmpegPath, arguments, timeout, ct);
    }

    /// <summary>
    /// Общая механика запуска: путь к исполняемому файлу приходит снаружи, чтобы
    /// поведение по таймауту можно было проверить тестом без ffmpeg в системе.
    /// </summary>
    public async Task RunProcessAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);

        logger.LogDebug(
            "Running {executable} with timeout {timeoutSeconds}s: {args}",
            Path.GetFileName(executablePath),
            timeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture),
            DescribeArguments(arguments));

        using var process = Process.Start(BuildStartInfo(executablePath, arguments))
            ?? throw new InvalidOperationException("Failed to start ffmpeg process.");

        // stderr читается без внешней отмены: после убийства процесса поток и так закроется,
        // а брошенное чтение оставило бы задачу с необработанным исключением.
        var stderrTask = ReadLimitedAsync(process.StandardError, StderrLogLimit);
        using var sampling = new CancellationTokenSource();
        var peakRssTask = SamplePeakRssAsync(process, sampling.Token);

        string stderr;
        try
        {
            await WaitForExitAsync(process, deadline.Token, ct);
        }
        finally
        {
            await sampling.CancelAsync();
            var peakRss = await peakRssTask;
            if (peakRss > 0)
            {
                metrics.RecordPeakChildRss(peakRss);
            }

            // Дочитываем всегда: брошенное чтение осталось бы висеть на закрытом потоке.
            stderr = await DrainAsync(stderrTask);
        }

        if (process.ExitCode != 0)
        {
            // Сырой stderr наружу не отдаём: в нём лежат абсолютные temp-пути и имя файла
            // пользователя, а исключение фоновой задачи целиком уходит в общий лог TaskQueue.
            var reason = FfmpegStderrSanitizer.Summarize(stderr, arguments);
            logger.LogError(
                "FFmpeg failed with exit code {exitCode}. Args: {args}. Reason: {reason}",
                process.ExitCode, DescribeArguments(arguments), reason);
            throw new InvalidOperationException($"FFmpeg failed with exit code {process.ExitCode}. {reason}");
        }
    }

    private static async Task<string> DrainAsync(Task<string> stderrTask)
    {
        try
        {
            return await stderrTask;
        }
        catch (Exception)
        {
            // Поток закрылся вместе с убитым процессом — читать больше нечего.
            return string.Empty;
        }
    }

    private async Task WaitForExitAsync(Process process, CancellationToken deadline, CancellationToken ct)
    {
        try
        {
            await process.WaitForExitAsync(deadline);
        }
        catch (OperationCanceledException)
        {
            KillTree(process);
            await process.WaitForExitAsync(CancellationToken.None);

            ct.ThrowIfCancellationRequested();
            throw new TimeoutException("FFmpeg exceeded the configured timeout and was terminated.");
        }
    }

    private void KillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to terminate ffmpeg process tree");
        }
    }

    private static ProcessStartInfo BuildStartInfo(string executablePath, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    /// <summary>Пути наружу не пишем: в диагностике полезны флаги, а не имена файлов.</summary>
    private static string DescribeArguments(IReadOnlyList<string> arguments) =>
        string.Join(' ', arguments.Select(argument => Path.IsPathRooted(argument) ? "<path>" : argument));

    /// <summary>
    /// Пиковый RSS ребёнка. Process.PeakWorkingSet64 на Linux не поддержан, поэтому
    /// подглядываем VmHWM в /proc, пока процесс жив; на других платформах метрики нет.
    /// </summary>
    private static async Task<long> SamplePeakRssAsync(Process process, CancellationToken stop)
    {
        if (!OperatingSystem.IsLinux())
        {
            return 0;
        }

        long peak = 0;
        while (!process.HasExited && !stop.IsCancellationRequested)
        {
            peak = Math.Max(peak, ReadPeakRssBytes(process.Id));
            try
            {
                await Task.Delay(RssSampleInterval, stop);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return peak;
    }

    private static long ReadPeakRssBytes(int pid)
    {
        try
        {
            foreach (var line in File.ReadLines($"/proc/{pid}/status"))
            {
                if (!line.StartsWith("VmHWM:", StringComparison.Ordinal))
                {
                    continue;
                }

                var kilobytes = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Skip(1)
                    .FirstOrDefault();
                return long.TryParse(kilobytes, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                    ? value * 1024
                    : 0;
            }
        }
        catch (IOException)
        {
            // Процесс успел завершиться между HasExited и чтением — это нормальный конец выборки.
        }

        return 0;
    }

    private static async Task<string> ReadLimitedAsync(StreamReader reader, int maxChars)
    {
        var buffer = new char[4096];
        var remaining = maxChars;
        var builder = new System.Text.StringBuilder(Math.Min(maxChars, 8192));
        var truncated = false;

        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length));
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
            builder.Append('…');
        }

        return builder.ToString();
    }
}
