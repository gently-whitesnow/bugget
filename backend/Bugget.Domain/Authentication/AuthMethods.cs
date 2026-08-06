namespace Bugget.Domain.Authentication;

/// <summary>
/// Способ аутентификации запроса. Значение claim <see cref="AuthClaims.AuthMethod"/>.
/// PAT-схема (P1) обязана выставлять <see cref="Pat"/> — от этого зависит
/// <see cref="Common.CreatorType.Agent"/>.
/// </summary>
public static class AuthMethods
{
    public const string Pat = "pat";
    public const string Jwt = "jwt";
}
