using Microsoft.AspNetCore.Authorization;

namespace Bugget.Api.Authentication;

/// <summary>
/// Аутентификация по хэдерам
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthAttribute : AuthorizeAttribute
{
    public AuthAttribute()
    {
        AuthenticationSchemes = AuthSchemeNames.Headers;
    }
}
