namespace Bugget.Api.Mcp;

/// <summary>Ответ create_report: репорт без вложенного дерева.</summary>
internal sealed record McpReportSummary(
    string Id,
    string Title,
    string Status,
    string CreatorUserId,
    string ResponsibleUserId,
    string? CreatorTeamId,
    string CreatorType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
