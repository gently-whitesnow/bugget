namespace Bugget.Api.Mcp;

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
