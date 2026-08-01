using System.Globalization;
using Bugget.Application.Interfaces;
using Bugget.Application.Ports;
using Bugget.Application.Services.Attachments;
using Bugget.Domain.Attachments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugget.Infrastructure.Attachments;

/// <summary>
/// Фоновая оптимизация видео: перекодирование в mp4 и превью. Загрузка к этому моменту
/// уже завершена и оригинал доступен, поэтому задача может ждать сколько нужно — важнее
/// удержать память процесса, чем ускорить кодирование (MAIN-188, MAIN-194).
/// </summary>
public sealed class VideoOptimizeWriter(
    IFileStorageClient fileStorageClient,
    IAttachmentKeyGenerator keyGen,
    FfmpegProcessRunner ffmpegRunner,
    VideoTranscodeGate transcodeGate,
    IOptions<OptimizatorSettings> opt,
    ILogger<VideoOptimizeWriter> logger)
{
    private int _profileLogged;

    public async Task<OptimizationResult> OptimizeWriteAsync(
        string? organizationId,
        int reportId,
        Attachment attachment,
        Stream originalStream,
        CancellationToken ct = default)
    {
        var settings = opt.Value;
        if (!settings.VideoOptimizationEnabled)
        {
            return await WriteOriginalAsync(organizationId, reportId, attachment, originalStream, ct);
        }

        LogProfileOnce(settings);

        // Слот берётся до подготовки временного входа: ожидающие задачи не должны
        // держать ни копии оригинала на диске, ни дочерних процессов.
        using var lease = await transcodeGate.AcquireAsync(ct);
        try
        {
            var result = await TranscodeAsync(organizationId, reportId, attachment, originalStream, settings, ct);
            lease.Complete(VideoOptimizeOutcome.Success);
            return result;
        }
        catch (OperationCanceledException)
        {
            lease.Complete(VideoOptimizeOutcome.Canceled);
            throw;
        }
        catch (TimeoutException)
        {
            lease.Complete(VideoOptimizeOutcome.Timeout);
            throw;
        }
    }

    private async Task<OptimizationResult> TranscodeAsync(
        string? organizationId,
        int reportId,
        Attachment attachment,
        Stream originalStream,
        OptimizatorSettings settings,
        CancellationToken ct)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "bugget-video", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        var inputPath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}{Path.GetExtension(attachment.FileName)}");
        var outputPath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.mp4");
        var previewPath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.webp");
        var timeout = TimeSpan.FromSeconds(settings.VideoTimeoutSeconds);

        try
        {
            await WriteStreamToFileAsync(originalStream, inputPath, ct);

            // Проверяем, что файл был записан и не пустой
            if (!File.Exists(inputPath) || new FileInfo(inputPath).Length == 0)
            {
                // Путь во временном каталоге наружу не отдаём — исключение фоновой задачи уходит в общий лог.
                throw new InvalidOperationException("Input file for transcoding was not created or is empty.");
            }

            await ffmpegRunner.RunAsync(BuildTranscodeArguments(settings, inputPath, outputPath), timeout, ct);
            await ffmpegRunner.RunAsync(BuildPreviewArguments(settings, outputPath, previewPath), timeout, ct);

            var storageKey = keyGen.GetOriginalKey(organizationId, reportId, attachment.EntityId, ".mp4");
            var previewKey = keyGen.GetPreviewKey(storageKey);

            await using var outputStream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var previewStream = new FileStream(previewPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Точка невозврата: перекодирование прервать можно, запись результата — уже нет.
            var persist = AttachmentPersistence.BeginPersisting(ct);
            await Task.WhenAll(
                fileStorageClient.WriteAsync(storageKey, outputStream, persist),
                fileStorageClient.WriteAsync(previewKey, previewStream, persist));

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

    /// <summary>
    /// Видеооптимизация выключена: оригинал переезжает из временного ключа в постоянный
    /// как есть. Так вложение не остаётся навсегда в состоянии «ждёт обработки».
    /// </summary>
    private async Task<OptimizationResult> WriteOriginalAsync(
        string? organizationId,
        int reportId,
        Attachment attachment,
        Stream originalStream,
        CancellationToken ct)
    {
        var storageKey = keyGen.GetOriginalKey(
            organizationId,
            reportId,
            attachment.EntityId,
            Path.GetExtension(attachment.FileName).ToLowerInvariant());

        // Выключенная оптимизация — тот же шов: прерваться можно только до начала записи.
        await fileStorageClient.WriteAsync(storageKey, originalStream, AttachmentPersistence.BeginPersisting(ct));

        return new OptimizationResult(
            FileName: attachment.FileName,
            StorageKey: storageKey,
            MimeType: attachment.MimeType,
            LengthBytes: originalStream.CanSeek ? originalStream.Length : attachment.LengthBytes ?? 0,
            IsGzipCompressed: false,
            HasPreview: false,
            PreviewLengthBytes: 0
        );
    }

    /// <summary>
    /// Потолки потоков — главный рычаг памяти, и их три, а не один: <c>-filter_threads</c>
    /// глобальный, <c>-threads</c> до <c>-i</c> ограничивает декодер, <c>-threads</c> перед
    /// выходом — кодировщик. Декодер самый дорогой: на 4K он и держал лишние сотни мегабайт.
    /// </summary>
    public static string[] BuildTranscodeArguments(OptimizatorSettings settings, string inputPath, string outputPath) =>
    [
        "-y",
        "-nostdin",
        "-filter_threads", Format(settings.VideoFilterThreads),
        "-threads", Format(settings.VideoDecoderThreads),
        "-i", inputPath,
        "-map_metadata", "-1",
        "-map", "0:v:0",
        "-map", "0:a:0?",
        "-sn",
        "-dn",
        "-vf", $"scale=min({Format(settings.VideoMaxWidth)}\\,iw):-2,setsar=1",
        "-metadata:s:v:0", "rotate=0",
        "-c:v", "libx264",
        "-preset", settings.VideoPreset,
        "-crf", Format(settings.VideoCrf),
        "-pix_fmt", "yuv420p",
        "-c:a", "aac",
        "-b:a", $"{Format(settings.VideoAudioBitrateKbps)}k",
        "-threads", Format(settings.VideoEncoderThreads),
        "-movflags", "+faststart",
        outputPath
    ];

    public static string[] BuildPreviewArguments(OptimizatorSettings settings, string inputPath, string previewPath) =>
    [
        "-y",
        "-nostdin",
        "-filter_threads", Format(settings.VideoFilterThreads),
        "-threads", Format(settings.VideoDecoderThreads),
        "-i", inputPath,
        "-frames:v", "1",
        "-vf", $"scale=min({Format(settings.MaxPreviewSize)}\\,iw):-2",
        "-threads", Format(settings.VideoEncoderThreads),
        "-f", "webp",
        previewPath
    ];

    /// <summary>Профиль пишется один раз на процесс: имён файлов и секретов в нём нет.</summary>
    private void LogProfileOnce(OptimizatorSettings settings)
    {
        if (Interlocked.Exchange(ref _profileLogged, 1) == 1)
        {
            return;
        }

        logger.LogInformation(
            "Video optimization profile: concurrency={concurrency}, encoder_threads={encoderThreads}, " +
            "decoder_threads={decoderThreads}, filter_threads={filterThreads}, timeout={timeoutSeconds}s, " +
            "crf={crf}, preset={preset}, width={width}",
            transcodeGate.MaxConcurrency,
            settings.VideoEncoderThreads,
            settings.VideoDecoderThreads,
            settings.VideoFilterThreads,
            settings.VideoTimeoutSeconds,
            settings.VideoCrf,
            settings.VideoPreset,
            settings.VideoMaxWidth);
    }

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static async Task WriteStreamToFileAsync(Stream stream, string path, CancellationToken ct)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await stream.CopyToAsync(output, ct);
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
