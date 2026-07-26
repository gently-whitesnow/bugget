namespace Users.Api.Controllers.Workspaces;

public sealed class WorkspacesContextView
{
    public required WorkspaceView[] Workspaces { get; set; }
    public required TeamMemberView[]? TeamsMember { get; set; }
    public required WorkspaceMemberView[]? WorkspacesMember { get; set; }
}
