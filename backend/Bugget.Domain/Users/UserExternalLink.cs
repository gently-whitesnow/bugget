namespace Bugget.Domain.Users;

public sealed class UserExternalLink
{
    public required long Id { get; set; }
    public required long UserId { get; set; }
    public required string Provider { get; set; }
    public required string ExternalId { get; set; }
    public string? Email { get; set; }
    public required DateTimeOffset LinkedAt { get; set; }
}
