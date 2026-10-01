namespace Bugget.Api.Mcp;

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
