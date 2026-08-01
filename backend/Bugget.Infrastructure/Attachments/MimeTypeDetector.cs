using System.Buffers;
using Bugget.Application.Ports;
using HeyRed.Mime;

namespace Bugget.Infrastructure.Attachments;

/// <summary>
/// Определение mime по сигнатуре содержимого через libmagic (пакет Mime).
/// Библиотека нативная и в прикладном слое ей не место — отсюда порт.
/// </summary>
public sealed class MimeTypeDetector : IMimeTypeDetector
{
    private const int DetectionBufferSize = 4 * 1024;

    public async Task<string> DetectAsync(Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Сохраняем позицию, если можем
        long origPos = content.CanSeek ? content.Position : 0;

        // Читаем в арендованный буфер
        var buffer = ArrayPool<byte>.Shared.Rent(DetectionBufferSize);
        try
        {
            int bytesRead = await content.ReadAsync(buffer.AsMemory(0, DetectionBufferSize), ct);

            // Сбрасываем позицию, если это возможно
            if (content.CanSeek)
            {
                content.Position = origPos;
            }

            var mime = MimeGuesser.GuessMimeType(buffer[..bytesRead]);
            return string.IsNullOrWhiteSpace(mime)
                ? "application/octet-stream"
                : mime;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
