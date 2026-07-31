using Bugget.BO.Mappers;
using Bugget.BO.Ports;
using Bugget.Entities.BO.AttachmentBo;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.Constants;
using Microsoft.Extensions.Logging;

namespace Bugget.BO.Services.Attachments;

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
        Attachment fromAttachment,
        CancellationToken ct = default)
    {
        if (fromAttachment.StorageKind != (int)StorageKind.Temp || fromAttachment.StorageKey is null)
        {
            return;
        }

        await using var fileStream = await fileStorage.ReadAsync(fromAttachment.StorageKey, ct);

        // Создаем новую оптимизированную версию файла
        var optimizationResult = await OptimizeByMimeTypeAsync(
            organizationId, reportIdContext, fromAttachment, fileStream, ct);

        LogOptimizationResult(fromAttachment, fileStream, optimizationResult);

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
        await fileStorage.DeleteAsync(fromAttachment.StorageKey, ct);
    }

    /// <summary>Выбор писателя по mime-типу: у всех троих один контракт и один токен отмены.</summary>
    private Task<OptimizationResult> OptimizeByMimeTypeAsync(
        string? organizationId,
        ReportIdContext reportIdContext,
        Attachment fromAttachment,
        Stream fileStream,
        CancellationToken ct)
    {
        if (AttachmentConstants.ImageMimeTypes.Contains(fromAttachment.MimeType, StringComparer.OrdinalIgnoreCase))
        {
            return imageOptimizatorWriter.OptimizeWriteAsync(
                organizationId, reportIdContext.ReportId, fromAttachment, fileStream, ct);
        }

        if (AttachmentConstants.VideoMimeTypes.Contains(fromAttachment.MimeType, StringComparer.OrdinalIgnoreCase))
        {
            return videoOptimizeWriter.OptimizeWriteAsync(
                organizationId, reportIdContext.ReportId, fromAttachment, fileStream, ct);
        }

        if (AttachmentConstants.CompressibleMimeTypes.Contains(fromAttachment.MimeType, StringComparer.OrdinalIgnoreCase))
        {
            return textOptimizator.OptimizeWriteAsync(
                organizationId, reportIdContext.ReportId, fromAttachment, fileStream, ct);
        }

        throw new InvalidOperationException("Unsupported mime type");
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
