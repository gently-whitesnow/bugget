using Bugget.Application.Mappers;
using Bugget.Application.Ports;
using Bugget.Domain.Attachments;
using Bugget.Domain.Constants;
using Bugget.Domain.Reports;
using Microsoft.Extensions.Logging;

namespace Bugget.Application.Services.Attachments;

public sealed class AttachmentOptimizator(
    IFileStorageClient fileStorage,
    TextOptimizeWriter textOptimizator,
    ImageOptimizeWriter imageOptimizatorWriter,
    VideoOptimizeWriter videoOptimizeWriter,
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

        // Создаем новую оптимизированную версию файла
        OptimizationResult optimizationResult;
        if (AttachmentConstants.ImageMimeTypes.Contains(fromAttachment.MimeType, StringComparer.OrdinalIgnoreCase))
        {
            optimizationResult = await imageOptimizatorWriter.OptimizeWriteAsync(
                organizationId,
                reportIdContext.ReportId,
                fromAttachment,
                fileStream
            );
        }
        else if (AttachmentConstants.VideoMimeTypes.Contains(fromAttachment.MimeType, StringComparer.OrdinalIgnoreCase))
        {
            optimizationResult = await videoOptimizeWriter.OptimizeWriteAsync(
                organizationId,
                reportIdContext.ReportId,
                fromAttachment,
                fileStream
            );
        }
        else if (AttachmentConstants.CompressibleMimeTypes.Contains(fromAttachment.MimeType, StringComparer.OrdinalIgnoreCase))
        {
            optimizationResult = await textOptimizator.OptimizeWriteAsync(
                organizationId,
                reportIdContext.ReportId,
                fromAttachment,
                fileStream
            );
        }
        else
        {
            throw new InvalidOperationException("Unsupported mime type");
        }

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
