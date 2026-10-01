using Bugget.Api.Generated.Reports;
using Bugget.Application.Ports;
using Bugget.Application.Services.Attachments;
using Bugget.Domain.Attachments;
using Bugget.Domain.Constants;
using Microsoft.AspNetCore.WebUtilities;

namespace Bugget.Api.Controllers.Attachments;

/// <summary>Общая подготовка загруженного файла для контроллеров вложений бага, шага и комментария.</summary>
internal static class AttachmentUploadReader
{
    private static readonly bool IsDevelopment =
        Environment.GetEnvironmentVariable(EnvironmentConstants.AspnetcoreEnvironment)?
            .Equals("development", StringComparison.OrdinalIgnoreCase) ?? false;

    /// <summary>MIME определяется по содержимому: заголовок клиента приходит снаружи, верить ему нельзя.
    /// В development берётся заявленный — иначе локально нужны настоящие файлы нужных форматов.</summary>
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

    public static async Task<IReadOnlyList<AttachmentUpload>> ReadManyAsync(
        IEnumerable<FileParameter>? files,
        IMimeTypeDetector mimeTypeDetector,
        CancellationToken cancellationToken)
    {
        var uploads = new List<AttachmentUpload>();
        foreach (var file in files ?? [])
        {
            var (content, meta) = await ReadAsync(file, mimeTypeDetector, cancellationToken);
            uploads.Add(new AttachmentUpload(content, meta));
        }

        return uploads;
    }

    /// <summary>Белый список сравнивает MIME целиком: <c>text/plain;charset=utf-8</c> от браузера
    /// без нормализации не прошёл бы как <c>text/plain</c>.</summary>
    internal static string StripMimeParameters(string contentType)
    {
        var separator = contentType.IndexOf(';');
        var mediaType = separator < 0 ? contentType : contentType[..separator];
        return mediaType.Trim();
    }
}
