namespace Users.Api.Controllers.Workspaces;

public sealed class WorkspaceView
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset UpdatedAt { get; set; }
    public TeamView[]? Teams { get; set; }
}
