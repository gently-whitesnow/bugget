using System.Security.Claims;
using System.Threading.Tasks;
using Bugget.Api.Authorization;
using Bugget.Api.Authorization.Extensions;
using Bugget.Api.Authorization.Interfaces;
using Bugget.Api.Authorization.Oidc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugget.Api.Authorization.Services;

public sealed class ConfigureOidcJwtBearerOptions(
    IOptions<OidcAuthOptions> oidcOptions,
    IUsersService usersService,
    ILogger<ConfigureOidcJwtBearerOptions> logger) : IConfigureNamedOptions<JwtBearerOptions>
{
    private const string OidcBearerScheme = AuthorizationSchemeNames.Jwt;

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != OidcBearerScheme)
        {
            return;
        }

        var oidc = oidcOptions.Value;
        var authority = oidc.Authority?.TrimEnd('/');
        options.Authority = authority;
        options.Audience = oidc.Audience;
        options.RequireHttpsMetadata = oidc.RequireHttpsMetadata;
        options.TokenValidationParameters.ValidIssuer = authority;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateLifetime = oidc.ValidateLifetime;
        options.TokenValidationParameters.ValidateAudience = oidc.ValidateAudience && !string.IsNullOrEmpty(oidc.Audience);

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = OidcTokenExtractor.Extract(ctx.Request, oidc);
                if (!string.IsNullOrEmpty(token))
                {
                    ctx.Token = token;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async ctx =>
            {
                var principal = ctx.Principal;
                if (principal == null)
                {
                    ctx.Fail("Principal is null");
                    return;
                }

                var idKey = string.IsNullOrEmpty(oidc.IdKey) ? "sub" : oidc.IdKey;
                var externalId = principal.FindFirstValue(idKey)
                    ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal.FindFirstValue("sub");
                if (string.IsNullOrEmpty(externalId))
                {
                    logger.LogWarning("OIDC Bearer: no IdKey claim {IdKey} in token", idKey);
                    ctx.Fail("Missing subject claim");
                    return;
                }

                var userContext = await usersService.GetUserByExternalIdAsync(externalId);
                if (userContext == null)
                {
                    logger.LogWarning("OIDC Bearer: user not found for externalId {ExternalId}", externalId);
                    ctx.Fail("User not found");
                    return;
                }

                ctx.HttpContext.SetAuthHeaders(userContext);
            }
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(OidcBearerScheme, options);
}
