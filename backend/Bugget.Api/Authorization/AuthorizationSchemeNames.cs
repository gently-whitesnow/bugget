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

    /// <summary>
    /// Bearer personal access token (<c>bgt_pat_*</c>).
    /// </summary>
    public const string Pat = "authorization-pat";

    /// <summary>
    /// Policy-схема для <c>/_internal/auth</c>: по формату Bearer выбирает JWT или PAT.
    /// </summary>
    public const string Internal = "authorization-internal";
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
