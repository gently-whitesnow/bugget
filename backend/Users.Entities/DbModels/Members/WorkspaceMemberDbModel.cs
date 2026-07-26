namespace Users.Entities.DbModels.Members;

public sealed class WorkspaceMemberDbModel
{
    public required int WorkspaceId { get; set; }
    public required long UserId { get; set; }
    public required string Role { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }

    public WorkspaceMemberDbModel[] ToArray()
    {
        throw new NotImplementedException();
    }
}
