namespace OidcAuth;

/// <summary>
/// Configuration for external OIDC provider token validation.
/// </summary>
public sealed class OidcAuthOptions
{
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// OIDC provider authority URL (e.g., https://keycloak.example.com/realms/myrealm).
    /// Used to discover JWKS endpoint via .well-known/openid-configuration.
    /// </summary>
    public string Authority { get; init; } = default!;

    /// <summary>
    /// Expected audience (client_id). If null, audience validation is skipped.
    /// </summary>
    public string? Audience { get; init; }

    /// <summary>
    /// Whether to validate the token audience. Default: true.
    /// Set to false when the Keycloak client_id is unknown or differs from Audience.
    /// </summary>
    public bool ValidateAudience { get; init; } = true;

    /// <summary>
    /// Header name containing Bearer token (e.g., "Authorization" or "X-Id-Token").
    /// If null, header extraction is skipped.
    /// </summary>
    public string? TokenHeaderName { get; init; }

    /// <summary>
    /// Cookie name containing the OIDC token (set by oauth2-proxy).
    /// Default: "_oauth2_proxy"
    /// </summary>
    public string CookieName { get; init; } = "_oauth2_proxy";

    /// <summary>
    /// Whether to validate token lifetime. Default: true.
    /// </summary>
    public bool ValidateLifetime { get; init; } = true;

    /// <summary>
    /// Whether to require HTTPS for OIDC metadata endpoint.
    /// Default: true. Set to false only for local development/testing.
    /// </summary>
    public bool RequireHttpsMetadata { get; init; } = true;

    /// <summary>
    /// Path to redirect after successful authorization.
    /// Used when 'next' query parameter is not provided.
    /// Default: "/"
    /// </summary>
    public string DefaultRedirectPath { get; init; } = "/";

    /// <summary>
    /// Claim type used to extract the user identifier (externalId) from the token.
    /// Default: "sub"
    /// </summary>
    public string IdKey { get; init; } = "sub";
}
