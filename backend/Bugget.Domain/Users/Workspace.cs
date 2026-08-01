namespace Bugget.Domain.Users;

public sealed class Workspace
{
    public required int Id { get; set; }
    public required string Name { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset UpdatedAt { get; set; }
    public Team[]? Teams { get; set; }
}
