namespace Users.Entities.DbModels.Members;

public sealed class TeamMemberDbModel
{
    public required int TeamId { get; set; }
    public required long UserId { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
}
