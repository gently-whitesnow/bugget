namespace Bugget.Api.Mcp;

/// <summary>Ответ create_bug: баг без вложенного дерева.</summary>
internal sealed record McpBugSummary(
    int Id,
    string? Title,
    string Status,
    string CreatorUserId,
    string CreatorType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Receive,
    string? Expect);
