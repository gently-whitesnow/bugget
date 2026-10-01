namespace Bugget.Api.Authorization.Oidc;

public sealed class OidcAuthOptions
{
    public bool Enabled { get; init; }

    /// <summary>OIDC authority URL (e.g. https://keycloak.example.com/realms/myrealm); JWKS is discovered via .well-known.</summary>
    public string Authority { get; init; } = default!;

    /// <summary>Expected audience (client_id). If null, audience validation is skipped.</summary>
    public string? Audience { get; init; }

    /// <summary>Set to false when the Keycloak client_id is unknown or differs from Audience.</summary>
    public bool ValidateAudience { get; init; } = true;

    /// <summary>Header with the Bearer token (e.g. "Authorization" or "X-Id-Token"). If null, header extraction is skipped.</summary>
    public string? TokenHeaderName { get; init; }

    /// <summary>Cookie with the OIDC token (set by oauth2-proxy).</summary>
    public string CookieName { get; init; } = "_oauth2_proxy";

    public bool ValidateLifetime { get; init; } = true;

    /// <summary>Set to false only for local development/testing.</summary>
    public bool RequireHttpsMetadata { get; init; } = true;

    /// <summary>Redirect path after successful authorization when the 'next' query parameter is absent.</summary>
    public string DefaultRedirectPath { get; init; } = "/";

    /// <summary>Claim type used to extract the user identifier (externalId) from the token.</summary>
    public string IdKey { get; init; } = "sub";
}
