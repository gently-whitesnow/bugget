namespace Users.Entities.DbModels.Workspaces;

public sealed class WorkspaceDbModel
{
    public required int Id { get; set; }
    public required string Name { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset UpdatedAt { get; set; }
}
