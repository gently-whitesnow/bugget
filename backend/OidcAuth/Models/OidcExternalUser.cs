using Authorization.Abstractions;

namespace OidcAuth.Models;

/// <summary>
/// IExternalUser implementation for OIDC users.
/// </summary>
public sealed record OidcExternalUser(string ExternalId) : IExternalUser
{
    public string? Name => null;
    public string? ImageUrl => null;
}
