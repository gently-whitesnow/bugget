using System;
using Microsoft.AspNetCore.Authorization;

namespace Authentication;

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

    public AuthAttribute(string roles)
    {
        AuthenticationSchemes = AuthSchemeNames.Headers;
        Roles = roles;
    }
}
