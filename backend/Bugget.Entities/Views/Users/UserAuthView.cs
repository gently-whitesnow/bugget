namespace Bugget.Entities.Views.Users;

public sealed class UserAuthView
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? ImageUrl { get; init; }
    public string? WorkspaceId { get; init; }
    public string? WorkspaceRole { get; init; }
    public string? TeamId { get; init; }
    public string? MattermostUserId { get; init; }
}
