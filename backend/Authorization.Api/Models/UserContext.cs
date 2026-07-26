using System;

namespace Authorization.Api.Models;

public sealed record UserContext(User User, WorkspaceMember[] Workspaces);

public sealed class User
{
    public long Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTimeOffset RegistrationDate { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record WorkspaceMember(
    int WorkspaceId,
    string Role,
    int[] TeamIds
);
