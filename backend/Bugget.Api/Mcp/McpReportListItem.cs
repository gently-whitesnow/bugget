namespace Bugget.Api.Mcp;

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
