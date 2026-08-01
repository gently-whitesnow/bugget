using Bugget.Application.Ports;
using Bugget.Domain.Attachments;
using Bugget.Domain.Constants;

namespace Bugget.Infrastructure.Attachments;

/// <summary>
/// Реализация пережатия вложений: выбирает писателя по типу содержимого.
/// Здесь же заканчиваются все зависимости от медиа-библиотек — ImageSharp для картинок,
/// ffmpeg для видео, GZip для текста.
/// </summary>
public sealed class AttachmentOptimizer(
    ImageOptimizeWriter imageWriter,
    VideoOptimizeWriter videoWriter,
    TextOptimizeWriter textWriter) : IAttachmentOptimizer
{
    public Task<OptimizationResult> OptimizeAsync(
        string? organizationId,
        int reportId,
        Attachment attachment,
        Stream original,
        CancellationToken ct = default)
    {
        if (AttachmentConstants.ImageMimeTypes.Contains(attachment.MimeType, StringComparer.OrdinalIgnoreCase))
        {
            return imageWriter.OptimizeWriteAsync(organizationId, reportId, attachment, original, ct);
        }

        if (AttachmentConstants.VideoMimeTypes.Contains(attachment.MimeType, StringComparer.OrdinalIgnoreCase))
        {
            return videoWriter.OptimizeWriteAsync(organizationId, reportId, attachment, original, ct);
        }

        if (AttachmentConstants.CompressibleMimeTypes.Contains(attachment.MimeType, StringComparer.OrdinalIgnoreCase))
        {
            return textWriter.OptimizeWriteAsync(organizationId, reportId, attachment, original, ct);
        }

        // Текст и тип исключения сохранены с момента, когда выбор писателя жил в
        // прикладном слое: на него смотрит обработка ошибок выше по стеку.
        throw new InvalidOperationException("Unsupported mime type");
    }
}
