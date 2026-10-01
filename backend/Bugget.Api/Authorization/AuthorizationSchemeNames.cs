using Microsoft.AspNetCore.Authorization;

namespace Bugget.Api.Authorization;

public static class AuthorizationSchemeNames
{
    /// <summary>
    /// Одно имя и для собственных JWT, и для OIDC: конфигуратор выбирает
    /// <c>OidcAuthOptions.Enabled</c>, а контроллерам нужна стабильная ссылка на схему.
    /// </summary>
    public const string Jwt = "authorization-jwt";

    /// <summary>Bearer personal access token (<c>bgt_pat_*</c>).</summary>
    public const string Pat = "authorization-pat";

    /// <summary>Policy-схема для <c>/_internal/auth</c>: по формату Bearer выбирает JWT или PAT.</summary>
    public const string Internal = "authorization-internal";
}
