namespace Bugget.Api.Users.Controllers.Workspaces;

public sealed class TeamMemberView
{
    public required string TeamId { get; set; }
    public required string UserId { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
}
