namespace Bugget.Api.Mcp;

/// <summary>
/// Проекции ответов read-инструментов.
///
/// Имена полей и строки статусов совпадают с REST, а состав — нет: то же дерево
/// целиком модель оплачивает токенами за каждый вызов. Поэтому список не тащит
/// баги, а вложения в дереве репорта отдают ровно то, по чему видно, стоит ли за
/// ними идти. Что отброшено против REST: past_responsible_user_id и
/// is_excluded_from_analytics (нужны аналитике, не читателю), report_id и
/// bug_id у вложенных сущностей (родитель известен из позиции в дереве),
/// updated_at у комментариев и шагов.
/// </summary>
internal sealed record McpReportPage(
    long Total,
    int Skip,
    int Take,
    McpReportListItem[] Reports);

internal sealed record McpReportListItem(
    string Id,
    string Title,
    string Status,
    string CreatorUserId,
    string ResponsibleUserId,
    string? CreatorTeamId,
    string CreatorType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int BugsCount);

internal sealed record McpReport(
    string Id,
    string Title,
    string Status,
    string CreatorUserId,
    string ResponsibleUserId,
    string? CreatorTeamId,
    string CreatorType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string[]? ParticipantsUserIds,
    McpReportLink[]? Links,
    McpBug[]? Bugs);

internal sealed record McpReportLink(string Name, string Link);

internal sealed record McpBug(
    int Id,
    string? Title,
    string Status,
    string CreatorUserId,
    string CreatorType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Receive,
    string? Expect,
    McpBugStep[]? Steps,
    McpComment[]? Comments,
    McpAttachment[]? Attachments);

internal sealed record McpBugStep(
    int Id,
    int StepNumber,
    string Text,
    McpAttachment[]? Attachments);

internal sealed record McpComment(
    int Id,
    string Text,
    string CreatorUserId,
    string CreatorType,
    string Audience,
    DateTimeOffset CreatedAt,
    McpAttachment[]? Attachments);

internal sealed record McpAttachment(
    int Id,
    string FileName,
    string AttachType,
    bool HasPreview);

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

/// <summary>
/// Пагинация текстового вложения: сколько символов всего, что отдано и остался
/// ли хвост. Поля явные — «обрезали молча» для модели неотличимо от «файл
/// закончился».
/// </summary>
internal sealed record McpTextPage(
    int TotalChars,
    int Offset,
    int ReturnedChars,
    bool Truncated);

/// <summary>
/// Ответ <c>patch_report</c> — та же проекция, что REST отдаёт из PATCH
/// (<c>ReportPatchResultViewModel</c>), статус строкой провода.
/// </summary>
internal sealed record McpReportPatchResult(
    string Id,
    string Title,
    string Status,
    string ResponsibleUserId,
    string PastResponsibleUserId,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Ответ <c>patch_bug</c> — колонки <c>BugPatchResult</c> как есть, статус
/// строкой провода.
/// </summary>
internal sealed record McpBugPatchResult(
    int Id,
    string? Title,
    string Status,
    string? Receive,
    string? Expect,
    DateTimeOffset UpdatedAt);
