namespace Bugget.Api.Mcp;

internal sealed record McpAttachment(
    int Id,
    string FileName,
    string AttachType,
    bool HasPreview);
