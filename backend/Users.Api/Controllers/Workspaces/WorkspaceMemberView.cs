namespace Users.Api.Controllers.Workspaces;

public sealed class WorkspaceMemberView
{
    public required string WorkspaceId { get; set; }
    public required string UserId { get; set; }
    public required string Role { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
}
