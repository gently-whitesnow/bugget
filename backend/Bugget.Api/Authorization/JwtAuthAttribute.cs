using Microsoft.AspNetCore.Authorization;

namespace Bugget.Api.Authorization;

/// <summary>
/// Аутентификация по JWT (cookie или Bearer) модуля authorization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class JwtAuthAttribute : AuthorizeAttribute
{
    public JwtAuthAttribute()
    {
        AuthenticationSchemes = AuthorizationSchemeNames.Jwt;
    }
}
