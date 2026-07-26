using System.Security.Claims;

namespace Bugget.Entities.Authentication;

public class UserIdentity(ClaimsPrincipal principal)
{
    public string Id { get; init; } = principal.Identity?.Name ?? "undefined_id";
    public string? TeamId { get; init; } = principal.FindFirst(AuthClaims.TeamId)?.Value;
    public string? OrganizationId { get; init; } = principal.FindFirst(AuthClaims.OrganizationId)?.Value;
    public string? SignalRConnectionId { get; init; } = principal.FindFirst(AuthClaims.SignalRConnectionId)?.Value;
}
