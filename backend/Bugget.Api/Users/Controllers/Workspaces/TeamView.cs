namespace Bugget.Api.Users.Controllers.Workspaces;

public sealed class TeamView
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset UpdatedAt { get; set; }
}
