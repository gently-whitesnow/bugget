namespace Bugget.Api.Mcp;

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
