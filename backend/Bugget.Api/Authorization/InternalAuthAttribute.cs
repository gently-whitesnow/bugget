using Microsoft.AspNetCore.Authorization;

namespace Bugget.Api.Authorization;

/// <summary>
/// Аутентификация nginx <c>auth_request</c>: JWT-сессия или PAT.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InternalAuthAttribute : AuthorizeAttribute
{
    public InternalAuthAttribute()
    {
        AuthenticationSchemes = AuthorizationSchemeNames.Internal;
    }
}
