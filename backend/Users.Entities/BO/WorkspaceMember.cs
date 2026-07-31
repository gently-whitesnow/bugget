namespace Users.Entities.BO;

public sealed class WorkspaceMember
{
    public required int WorkspaceId { get; set; }
    public required long UserId { get; set; }
    public required string Role { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }

    public WorkspaceMember[] ToArray()
    {
        throw new NotImplementedException();
    }
}
