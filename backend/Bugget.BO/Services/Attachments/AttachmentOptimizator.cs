using Bugget.BO.Mappers;
using Bugget.DA.Interfaces;
using Bugget.DA.WebSockets;
using Bugget.Entities.BO.AttachmentBo;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.Constants;
using Bugget.Entities.DbModels.Attachment;
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
        AttachmentDbModel fromAttachmentDbModel)
    {
        if (fromAttachmentDbModel.StorageKind != (int)StorageKind.Temp || fromAttachmentDbModel.StorageKey is null)
        {
            return;
        }

        await using var fileStream = await fileStorage.ReadAsync(fromAttachmentDbModel.StorageKey);

        // Создаем новую оптимизированную версию файла
        OptimizationResult optimizationResult;
        if (AttachmentConstants.ImageMimeTypes.Contains(fromAttachmentDbModel.MimeType, StringComparer.OrdinalIgnoreCase))
        {
            optimizationResult = await imageOptimizatorWriter.OptimizeWriteAsync(
                organizationId,
                reportIdContext.ReportId,
                fromAttachmentDbModel,
                fileStream
            );
        }
        else if (AttachmentConstants.VideoMimeTypes.Contains(fromAttachmentDbModel.MimeType, StringComparer.OrdinalIgnoreCase))
        {
            optimizationResult = await videoOptimizeWriter.OptimizeWriteAsync(
                organizationId,
                reportIdContext.ReportId,
                fromAttachmentDbModel,
                fileStream
            );
        }
        else if (AttachmentConstants.CompressibleMimeTypes.Contains(fromAttachmentDbModel.MimeType, StringComparer.OrdinalIgnoreCase))
        {
            optimizationResult = await textOptimizator.OptimizeWriteAsync(
                organizationId,
                reportIdContext.ReportId,
                fromAttachmentDbModel,
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
                fromAttachmentDbModel.FileName,
                originalLength.Value,
                optimizationResult.LengthBytes,
                optimizationResult.PreviewLengthBytes,
                (1 - (double)optimizationResult.LengthBytes / originalLength.Value) * 100);
        }
        else
        {
            logger.LogInformation("Attachment saved: {@FileName}, size {@to}-{@preview}",
                fromAttachmentDbModel.FileName,
                optimizationResult.LengthBytes,
                optimizationResult.PreviewLengthBytes);
        }

        // Обновляем модель в БД
        var toAttachmentDbModel = await attachmentDbClient.UpdateAttachmentAsync(new UpdateAttachmentDbModel
        {
            Id = fromAttachmentDbModel.Id,
            StorageKey = optimizationResult.StorageKey,
            StorageKind = (int)StorageKind.Standard,
            LengthBytes = optimizationResult.LengthBytes + optimizationResult.PreviewLengthBytes,
            FileName = optimizationResult.FileName,
            MimeType = optimizationResult.MimeType,
            HasPreview = optimizationResult.HasPreview,
            IsGzipCompressed = optimizationResult.IsGzipCompressed,
        });

        // Уведомляем клиентов
        await reportPageHubClient.SendAttachmentChangedAsync(reportIdContext.GroupKey, toAttachmentDbModel.ToSocketView());

        // Удаляем старый файл
        await fileStorage.DeleteAsync(fromAttachmentDbModel.StorageKey);
    }
}
