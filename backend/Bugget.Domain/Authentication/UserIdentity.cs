using System.Security.Claims;
using Bugget.Domain.Common;

namespace Bugget.Domain.Authentication;

public class UserIdentity(ClaimsPrincipal principal)
{
    public string Id { get; init; } = principal.Identity?.Name ?? "undefined_id";
    public string? TeamId { get; init; } = principal.FindFirst(AuthClaims.TeamId)?.Value;
    public string? OrganizationId { get; init; } = principal.FindFirst(AuthClaims.OrganizationId)?.Value;
    public string? SignalRConnectionId { get; init; } = principal.FindFirst(AuthClaims.SignalRConnectionId)?.Value;

    /// <summary>
    /// Как аутентифицировались: <see cref="AuthMethods.Pat"/> / <see cref="AuthMethods.Jwt"/> / null.
    /// </summary>
    public string? AuthMethod { get; init; } = principal.FindFirst(AuthClaims.AuthMethod)?.Value;

    /// <summary>
    /// Тип автора для записей, которые создаёт этот актор.
    /// </summary>
    public CreatorType ActorCreatorType { get; init; } =
        ActorCreatorTypes.FromAuthMethod(principal.FindFirst(AuthClaims.AuthMethod)?.Value);
}
