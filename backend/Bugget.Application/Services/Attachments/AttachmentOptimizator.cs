using Bugget.Application.Mappers;
using Bugget.Application.Ports;
using Bugget.Domain.Attachments;
using Bugget.Domain.Constants;
using Bugget.Domain.Reports;
using Microsoft.Extensions.Logging;

namespace Bugget.Application.Services.Attachments;

public sealed class AttachmentOptimizator(
    IFileStorageClient fileStorage,
    IAttachmentOptimizer optimizer,
    IAttachmentDbClient attachmentDbClient,
    ILogger<AttachmentOptimizator> logger,
    IReportPageHubClient reportPageHubClient
    )
{

    public static bool CanOptimize(string mimeType) =>
        AttachmentConstants.CompressibleMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase) ||
        AttachmentConstants.ImageMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase) ||
        AttachmentConstants.VideoMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase);

    public async Task OptimizeAttachmentAsync(
        string? organizationId,
        ReportIdContext reportIdContext,
        Attachment fromAttachment,
        CancellationToken ct = default)
    {
        if (fromAttachment.StorageKind != (int)StorageKind.Temp || fromAttachment.StorageKey is null)
        {
            return;
        }

        await using var fileStream = await fileStorage.ReadAsync(fromAttachment.StorageKey, ct);

        // Создаём новую оптимизированную версию файла: чем именно пережимать, решает
        // реализация порта — прикладной слой в медиа-форматы не лезет. Отменяемо: до
        // начала постоянной записи внутри писателя прервать работу можно (MAIN-243).
        var optimizationResult = await optimizer.OptimizeAsync(
            organizationId,
            reportIdContext.ReportId,
            fromAttachment,
            fileStream,
            ct);

        LogOptimizationResult(fromAttachment, fileStream, optimizationResult);

        // Писатель уже перешёл точку невозврата: результат лежит под постоянным ключом.
        // Остаток цепочки — только изменяющие вызовы, и они обязаны довыполниться, иначе
        // вложение останется с записанным файлом и строкой в БД на удалённый temp-ключ.
        var persist = AttachmentPersistence.Persisting;

        // Обновляем модель в БД
        var toAttachment = await attachmentDbClient.UpdateAttachmentAsync(new AttachmentUpdate
        {
            Id = fromAttachment.Id,
            StorageKey = optimizationResult.StorageKey,
            StorageKind = (int)StorageKind.Standard,
            LengthBytes = optimizationResult.LengthBytes + optimizationResult.PreviewLengthBytes,
            FileName = optimizationResult.FileName,
            MimeType = optimizationResult.MimeType,
            HasPreview = optimizationResult.HasPreview,
            IsGzipCompressed = optimizationResult.IsGzipCompressed,
        });

        // Уведомляем клиентов
        await reportPageHubClient.SendAttachmentChangedAsync(reportIdContext.GroupKey, toAttachment.ToSocketView());

        // Удаляем старый файл
        await fileStorage.DeleteAsync(fromAttachment.StorageKey, persist);
    }

    private void LogOptimizationResult(Attachment fromAttachment, Stream fileStream, OptimizationResult result)
    {
        var originalLength = fileStream.CanSeek ? fileStream.Length : (long?)null;
        if (originalLength is > 0)
        {
            logger.LogInformation("Attachment saved: {@FileName}, compress score {@from}-{@to}-{@preview} percent {@percent}%",
                fromAttachment.FileName,
                originalLength.Value,
                result.LengthBytes,
                result.PreviewLengthBytes,
                (1 - (double)result.LengthBytes / originalLength.Value) * 100);
            return;
        }

        logger.LogInformation("Attachment saved: {@FileName}, size {@to}-{@preview}",
            fromAttachment.FileName,
            result.LengthBytes,
            result.PreviewLengthBytes);
    }
}
