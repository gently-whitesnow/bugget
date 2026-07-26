namespace Authorization.Api.Models;

public sealed record AuthUserView(
    string Id,
    int? TeamId,
    int? WorkspaceId,
    string WorkspaceRole
);
