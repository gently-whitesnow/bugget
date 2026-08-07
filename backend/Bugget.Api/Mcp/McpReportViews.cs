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
/// Ответ <c>get_attachment</c>: те же поля, что REST отдаёт в
/// <c>AttachmentSummary</c>, плюс репорт, в котором вложение нашлось. Размер,
/// mime-тип и ключ хранилища REST наружу не отдаёт, и MCP — не то место, где это
/// решение отменяется мимоходом: чем модель платит за само содержимое, решает
/// P2d.
/// </summary>
internal sealed record McpAttachmentDetails(
    int Id,
    string ReportId,
    int EntityId,
    string AttachType,
    string FileName,
    bool HasPreview,
    DateTimeOffset CreatedAt,
    string CreatorUserId);
