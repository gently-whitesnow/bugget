namespace Users.Entities.BO;

public sealed class Team
{
    public required int Id { get; set; }
    public required int WorkspaceId { get; set; }
    public required string Name { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset UpdatedAt { get; set; }
}
