namespace Bugget.Api.Mcp;

internal sealed record McpComment(
    int Id,
    string Text,
    string CreatorUserId,
    string CreatorType,
    string Audience,
    DateTimeOffset CreatedAt,
    McpAttachment[]? Attachments);
