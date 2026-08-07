using Bugget.Api.Authorization.Authentication;
using Bugget.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bugget.Api.Authorization.Extensions;

internal static class InternalAuthenticationExtensions
{
    /// <summary>
    /// Policy Internal: Bearer <c>bgt_pat_*</c> → PAT, иначе JWT (cookie или Bearer).
    /// </summary>
    public static void AddInternalAuthSchemes(this AuthenticationBuilder authentication)
    {
        authentication.AddPolicyScheme(AuthorizationSchemeNames.Internal, "JWT or PAT", options =>
        {
            options.ForwardDefaultSelector = SelectScheme;
        });
        authentication.AddScheme<AuthenticationSchemeOptions, PersonalAccessTokenAuthenticationHandler>(
            AuthorizationSchemeNames.Pat,
            _ => { });
        authentication.AddJwtBearer(AuthorizationSchemeNames.Jwt, _ => { });
    }

    private static string SelectScheme(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        const string bearer = "Bearer ";
        if (authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            var token = authorization[bearer.Length..].Trim();
            if (PersonalAccessTokenSecret.HasValidFormat(token))
            {
                return AuthorizationSchemeNames.Pat;
            }
        }

        return AuthorizationSchemeNames.Jwt;
    }
}
