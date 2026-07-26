namespace Users.Entities.DbModels.Teams;

public sealed class TeamDbModel
{
    public required int Id { get; set; }
    public required int WorkspaceId { get; set; }
    public required string Name { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset UpdatedAt { get; set; }
}
