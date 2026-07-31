using Microsoft.AspNetCore.Authorization;

namespace Bugget.Api.Authorization;

public static class AuthorizationSchemeNames
{
    /// <summary>
    /// Схема JWT-аутентификации модуля authorization.
    /// Одно имя и для собственных токенов, и для OIDC: какой конфигуратор
    /// применится, решает <c>OidcAuthOptions.Enabled</c>, а контроллерам
    /// нужна стабильная ссылка на схему.
    /// </summary>
    public const string Jwt = "authorization-jwt";
}

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
