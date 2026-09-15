using Bugget.Api.Generated.Reports;
using Bugget.Application.Ports;
using Bugget.Domain.Attachments;
using Bugget.Domain.Constants;
using Microsoft.AspNetCore.WebUtilities;

namespace Bugget.Api.Controllers.Attachments;

/// <summary>
/// Общая подготовка загруженного файла для трёх контроллеров вложений: бага,
/// шага и комментария. Раньше этот кусок был скопирован в каждый из них.
/// </summary>
internal static class AttachmentUploadReader
{
    private static readonly bool IsDevelopment =
        Environment.GetEnvironmentVariable(EnvironmentConstants.AspnetcoreEnvironment)?
            .Equals("development", StringComparison.OrdinalIgnoreCase) ?? false;

    /// <summary>
    /// Возвращает перематываемый поток файла и его метаданные.
    /// </summary>
    /// <remarks>
    /// MIME определяется по содержимому, а не по заголовку клиента: заголовку
    /// нельзя верить, он приходит снаружи. В development берётся заявленный —
    /// иначе локальная разработка требует настоящих файлов нужных форматов.
    /// </remarks>
    public static async Task<(Stream Content, FileMeta Meta)> ReadAsync(
        FileParameter file,
        IMimeTypeDetector mimeTypeDetector,
        CancellationToken cancellationToken)
    {
        var content = file.Data;
        if (!content.CanSeek)
        {
            // Определение MIME читает начало потока и перематывает его назад,
            // поэтому неперематываемый поток буферизуем (крупный — на диск).
            var buffer = new FileBufferingReadStream(
                content,
                1024 * 1024,
                8 * 1024,
                Path.GetTempPath());

            await content.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            content = buffer;
        }

        var mimeType = IsDevelopment
            ? StripMimeParameters(file.ContentType)
            : await mimeTypeDetector.DetectAsync(content, cancellationToken);

        return (content, new FileMeta(file.FileName, content.Length, mimeType));
    }

    /// <summary>
    /// Белый список сравнивает MIME целиком, поэтому <c>text/plain;charset=utf-8</c>
    /// от браузера не проходил бы как <c>text/plain</c>.
    /// </summary>
    internal static string StripMimeParameters(string contentType)
    {
        var separator = contentType.IndexOf(';');
        var mediaType = separator < 0 ? contentType : contentType[..separator];
        return mediaType.Trim();
    }
}
