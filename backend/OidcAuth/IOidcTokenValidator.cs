using System.Security.Claims;

namespace OidcAuth;

public interface IOidcTokenValidator
{
    /// <summary>
    /// Validates an OIDC JWT token and returns the ClaimsPrincipal if valid.
    /// </summary>
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Extracts the subject (external user ID) from a validated token.
    /// </summary>
    string? GetSubject(ClaimsPrincipal principal);
}
