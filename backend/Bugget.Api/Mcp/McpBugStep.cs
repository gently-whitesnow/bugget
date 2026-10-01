namespace Bugget.Api.Mcp;

internal sealed record McpBugStep(
    int Id,
    int StepNumber,
    string Text,
    McpAttachment[]? Attachments);
