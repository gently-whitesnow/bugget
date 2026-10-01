namespace Bugget.Api.Mcp;

/// <summary>
/// Метаданные ответа <c>get_attachment</c>. Mime-тип и размер здесь появились
/// вместе с содержимым (P2d): по ним модель решает, запрашивать ли оригинал, —
/// это и есть та цена, которую P2b оставлял на решение P2d. Ключ хранилища
/// по-прежнему не уходит. <c>download_path</c> — внешний путь REST-скачивания
/// для человека: модель байты видео не получает никогда.
/// </summary>
internal sealed record McpAttachmentDetails(
    int Id,
    string ReportId,
    int EntityId,
    string AttachType,
    string FileName,
    string MimeType,
    long? LengthBytes,
    bool HasPreview,
    string DownloadPath,
    DateTimeOffset CreatedAt,
    string CreatorUserId);
