using System.Buffers;
using System.IO.Compression;
using Bugget.BO.Interfaces;
using Bugget.BO.Ports;
using Bugget.Entities.BO.AttachmentBo;

namespace Bugget.BO.Services.Attachments;

public sealed class TextOptimizeWriter(
    IFileStorageClient fileStorage,
    IAttachmentKeyGenerator keyGen
)
{
    private const int BufferSize = 128 * 1024; // 128 KiB из пула

    public async Task<OptimizationResult> OptimizeWriteAsync(
        string? organizationId,
        int reportId,
        Attachment attachment,
        Stream originalStream,
        CancellationToken ct = default)
    {
        // 1) Сброс позиции, если возможно
        if (originalStream.CanSeek)
        {
            originalStream.Position = 0;
        }

        // 2) Готовим ключ в хранилище
        var ext = Path.GetExtension(attachment.FileName);
        var storageKey = keyGen.GetOriginalKey(
            organizationId,
            reportId,
            attachment.EntityId,
            ext);

        // 3) Сжимаем в память, читая из пула
        await using var compressedMs = new MemoryStream();
        await using (var gzip = new GZipStream(
            compressedMs,
            CompressionLevel.Optimal,
            leaveOpen: true))
        {
            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                int read;
                while ((read = await originalStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await gzip.WriteAsync(buffer, 0, read, ct);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        compressedMs.Position = 0;

        // 4) Пишем в файловое хранилище — точка невозврата, дальше отмены нет
        await fileStorage.WriteAsync(storageKey, compressedMs, AttachmentPersistence.BeginPersisting(ct));

        // 5) Возвращаем информацию о результате
        return new OptimizationResult(
            FileName: attachment.FileName,
            StorageKey: storageKey,
            MimeType: attachment.MimeType,
            LengthBytes: compressedMs.Length,
            IsGzipCompressed: true,
            HasPreview: false,
            PreviewLengthBytes: 0);
    }
}
