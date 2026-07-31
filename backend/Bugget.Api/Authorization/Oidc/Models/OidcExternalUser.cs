using Bugget.Api.Authorization.Abstractions;

namespace Bugget.Api.Authorization.Oidc.Models;

/// <summary>
/// IExternalUser implementation for OIDC users.
/// </summary>
public sealed record OidcExternalUser(string ExternalId) : IExternalUser
{
    public string? Name => null;
    public string? ImageUrl => null;
}
