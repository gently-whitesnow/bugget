namespace Users.Entities.DbModels.Teams;

public sealed class TeamInviteDbModel
{
    public required int Id { get; set; }
    public required int TeamId { get; set; }
    public required int WorkspaceId { get; set; }
    public required byte[] TokenHash { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
