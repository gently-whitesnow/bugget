namespace Users.Entities.BO;

public sealed class TeamMember
{
    public required int TeamId { get; set; }
    public required long UserId { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
}
