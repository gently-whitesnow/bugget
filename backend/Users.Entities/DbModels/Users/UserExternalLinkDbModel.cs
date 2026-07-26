namespace Users.Entities.DbModels.Users;

public sealed class UserExternalLinkDbModel
{
    public required long Id { get; set; }
    public required long UserId { get; set; }
    public required string Provider { get; set; }
    public required string ExternalId { get; set; }
    public string? Email { get; set; }
    public required DateTimeOffset LinkedAt { get; set; }
}
