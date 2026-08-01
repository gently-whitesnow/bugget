namespace Bugget.Domain.Users;

public sealed class User
{
    public required long Id { get; set; }
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
    public string? ImageUrl { get; set; }
    public string? MattermostUserId { get; set; }
    public required DateTimeOffset RegistrationDate { get; set; }
    public required DateTimeOffset UpdatedAt { get; set; }
}
