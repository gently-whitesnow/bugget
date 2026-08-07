using System.IO.Compression;
using System.Text;
using Bugget.Application.Services.Attachments;
using Bugget.Domain;
using Bugget.Domain.Attachments;
using Bugget.Domain.Authentication;
using Bugget.Domain.Constants;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Bugget.Api.Mcp;

/// <summary>
/// Выдача содержимого вложения инструменту <c>get_attachment</c>: что именно и в
/// каком виде уходит модели. Правила экономии токенов зашиты здесь, а не
/// оставлены на усмотрение вызывающего:
///
/// - картинки — превью по умолчанию, оригинал только по явному флагу;
/// - видео — байты не уходят никогда, только кадр-превью и ссылка для человека;
/// - текст — как есть, без перекодировок, с пагинацией по символам.
///
/// Русский текст не конвертируется: наружу он уходит отдельным текстовым блоком
/// JSON-RPC, а не внутри сериализованного JSON, поэтому экранирование
/// <c>\uXXXX</c> ему не грозит вовсе.
/// </summary>
internal sealed class McpAttachmentContent(IAttachmentService attachmentService)
{
    /// <summary>
    /// Потолок и умолчание страницы текста — в символах, потому что токены модель
    /// платит за символы, а не за байты хранилища.
    /// </summary>
    public const int DefaultMaxChars = 20_000;

    public const int MaxMaxChars = 50_000;

    public async Task<List<ContentBlock>> BuildAsync(
        UserIdentity user,
        string reportId,
        LocatedAttachment located,
        bool original,
        int offset,
        int maxChars)
    {
        var attachment = located.Attachment;

        if (IsText(attachment.MimeType))
        {
            return await BuildTextAsync(user, reportId, located, offset, maxChars);
        }

        if (IsVideo(attachment.MimeType))
        {
            if (original)
            {
                throw new McpException(
                    "Байты видео через MCP не отдаются: модель их не прочитает, а токены сгорят. " +
                    "Человеку — ссылка download_path из метаданных; кадр-превью приходит по умолчанию.");
            }

            return await BuildPreviewOrNoteAsync(
                user, reportId, located,
                "Кадр-превью видео ещё готовится — запросите вложение позже.");
        }

        if (original)
        {
            var (content, error) = await ReadOriginalAsync(user, reportId, located);
            if (error is not null || content is null)
            {
                throw new McpException(error?.Title ?? "Вложение не прочиталось.");
            }

            return [ImageBlock(await BytesAsync(content.Value.Content), attachment.MimeType)];
        }

        return await BuildPreviewOrNoteAsync(
            user, reportId, located,
            "Превью ещё готовится — запросите вложение позже либо оригинал через original=true.");
    }

    public static bool IsText(string mimeType) =>
        AttachmentConstants.CompressibleMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase);

    public static bool IsVideo(string mimeType) =>
        AttachmentConstants.VideoMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase);

    public static void ValidateTextPaging(int offset, int maxChars)
    {
        if (offset < 0)
        {
            throw new McpException("Параметр offset не может быть отрицательным.");
        }

        if (maxChars is < 1 or > MaxMaxChars)
        {
            throw new McpException($"Параметр maxChars должен быть от 1 до {MaxMaxChars}.");
        }
    }

    /// <summary>
    /// Текст читается целиком, чтобы честно посчитать <c>total_chars</c>: файл в
    /// хранилище может лежать gzip'ом, и его длина в байтах о символах не говорит.
    /// Потолок размера текстовых вложений держит загрузка, а не это чтение.
    /// </summary>
    private async Task<List<ContentBlock>> BuildTextAsync(
        UserIdentity user,
        string reportId,
        LocatedAttachment located,
        int offset,
        int maxChars)
    {
        var (content, error) = await ReadOriginalAsync(user, reportId, located);
        if (error is not null || content is null)
        {
            throw new McpException(error?.Title ?? "Вложение не прочиталось.");
        }

        var (stream, model) = content.Value;
        await using var source = model.IsGzipCompressed == true
            ? new GZipStream(stream, CompressionMode.Decompress)
            : stream;
        using var reader = new StreamReader(source, Encoding.UTF8);
        var text = await reader.ReadToEndAsync();

        var page = offset >= text.Length
            ? string.Empty
            : text.Substring(offset, Math.Min(maxChars, text.Length - offset));

        return
        [
            new TextContentBlock
            {
                Text = McpWire.Serialize(new McpTextPage(
                    text.Length,
                    offset,
                    page.Length,
                    Truncated: offset + page.Length < text.Length)),
            },
            new TextContentBlock { Text = page },
        ];
    }

    private async Task<List<ContentBlock>> BuildPreviewOrNoteAsync(
        UserIdentity user,
        string reportId,
        LocatedAttachment located,
        string notReadyNote)
    {
        if (located.Attachment.HasPreview != true)
        {
            return [new TextContentBlock { Text = notReadyNote }];
        }

        var (preview, error) = await ReadPreviewAsync(user, reportId, located);
        if (error is not null || preview is null)
        {
            throw new McpException(error?.Title ?? "Превью не прочиталось.");
        }

        return [ImageBlock(await BytesAsync(preview), AttachmentConstants.PreviewMimeType)];
    }

    private Task<((Stream Content, Attachment Model)? Value, Bugget.Domain.Errors.Error? Error)> ReadOriginalAsync(
        UserIdentity user,
        string reportId,
        LocatedAttachment located) =>
        (AttachType)located.Attachment.AttachType switch
        {
            AttachType.Comment => attachmentService.GetCommentAttachmentContentAsync(
                user, reportId, located.BugId, located.ParentId, located.Attachment.Id),
            AttachType.BugStep => attachmentService.GetBugStepAttachmentContentAsync(
                user, reportId, located.BugId, located.ParentId, located.Attachment.Id),
            _ => attachmentService.GetBugAttachmentContentAsync(
                user, reportId, located.BugId, located.Attachment.Id),
        };

    private Task<(Stream? Value, Bugget.Domain.Errors.Error? Error)> ReadPreviewAsync(
        UserIdentity user,
        string reportId,
        LocatedAttachment located) =>
        (AttachType)located.Attachment.AttachType switch
        {
            AttachType.Comment => attachmentService.GetCommentAttachmentPreviewContentAsync(
                user, reportId, located.BugId, located.ParentId, located.Attachment.Id),
            AttachType.BugStep => attachmentService.GetBugStepAttachmentPreviewContentAsync(
                user, reportId, located.BugId, located.ParentId, located.Attachment.Id),
            _ => attachmentService.GetBugAttachmentPreviewContentAsync(
                user, reportId, located.BugId, located.Attachment.Id),
        };

    // Data у ContentBlock — base64-строка в виде UTF-8 байт, не сырые байты файла.
    private static ImageContentBlock ImageBlock(byte[] bytes, string mimeType) =>
        new() { Data = Encoding.UTF8.GetBytes(Convert.ToBase64String(bytes)), MimeType = mimeType };

    private static async Task<byte[]> BytesAsync(Stream stream)
    {
        await using (stream)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return buffer.ToArray();
        }
    }
}

/// <summary>
/// Вложение вместе с координатами родителя в дереве репорта: сервисные методы
/// чтения требуют bugId и идентификатор комментария либо шага.
/// </summary>
/// <param name="Attachment">Само вложение из дерева репорта.</param>
/// <param name="BugId">Баг, в поддереве которого нашлось вложение.</param>
/// <param name="ParentId">Комментарий или шаг; для вложения самого бага не используется.</param>
internal sealed record LocatedAttachment(Attachment Attachment, int BugId, int ParentId);
