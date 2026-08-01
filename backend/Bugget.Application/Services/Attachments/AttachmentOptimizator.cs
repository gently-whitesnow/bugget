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
        Attachment fromAttachment)
    {
        if (fromAttachment.StorageKind != (int)StorageKind.Temp || fromAttachment.StorageKey is null)
        {
            return;
        }

        await using var fileStream = await fileStorage.ReadAsync(fromAttachment.StorageKey);

        // Создаём новую оптимизированную версию файла: чем именно пережимать, решает
        // реализация порта — прикладной слой в медиа-форматы не лезет.
        var optimizationResult = await optimizer.OptimizeAsync(
            organizationId,
            reportIdContext.ReportId,
            fromAttachment,
            fileStream);

        var originalLength = fileStream.CanSeek ? fileStream.Length : (long?)null;
        if (originalLength.HasValue && originalLength.Value > 0)
        {
            logger.LogInformation("Attachment saved: {@FileName}, compress score {@from}-{@to}-{@preview} percent {@percent}%",
                fromAttachment.FileName,
                originalLength.Value,
                optimizationResult.LengthBytes,
                optimizationResult.PreviewLengthBytes,
                (1 - (double)optimizationResult.LengthBytes / originalLength.Value) * 100);
        }
        else
        {
            logger.LogInformation("Attachment saved: {@FileName}, size {@to}-{@preview}",
                fromAttachment.FileName,
                optimizationResult.LengthBytes,
                optimizationResult.PreviewLengthBytes);
        }

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
        await fileStorage.DeleteAsync(fromAttachment.StorageKey);
    }
}
