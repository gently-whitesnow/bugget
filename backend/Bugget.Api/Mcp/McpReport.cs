namespace Bugget.Api.Mcp;

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
