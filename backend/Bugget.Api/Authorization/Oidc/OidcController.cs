using Bugget.Api.Authorization.Abstractions;
using Bugget.Api.Authorization.Oidc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugget.Api.Authorization.Oidc;

[ApiController, Route("v1/external/token")]
public sealed class OidcController(
    IOidcTokenValidator tokenValidator,
    IExternalAuthService externalAuth,
    IOptions<OidcAuthOptions> oidcOptions,
    ILogger<OidcController> logger) : ControllerBase
{
    private const string Provider = "oidc";
    private readonly OidcAuthOptions _options = oidcOptions.Value;
    private readonly string _domain = Environment.GetEnvironmentVariable("APP_DOMAIN")
        ?? throw new InvalidOperationException("APP_DOMAIN is not set");

    /// <summary>Callback после OIDC авторизации через oauth2-proxy: валидирует токен, привязывает identity, редиректит на next.</summary>
    /// <remarks>
    /// Маршрут анонимный по замыслу: вызывающего ещё нет в базе — он тут и заводится, —
    /// а доверие даёт не сессия, а токен провайдера, который валидируется ниже.
    /// </remarks>
    [AllowAnonymous, HttpGet("callback")]
    public async Task<IActionResult> CallbackAsync()
    {
        var token = ExtractToken();
        if (string.IsNullOrEmpty(token))
        {
            logger.LogWarning("OIDC redirect: no token found");
            return Unauthorized();
        }

        var principal = await tokenValidator.ValidateTokenAsync(token, HttpContext.RequestAborted);
        if (principal == null)
        {
            logger.LogWarning("OIDC redirect: token validation failed");
            return Unauthorized();
        }

        var externalId = tokenValidator.GetSubject(principal);
        if (string.IsNullOrEmpty(externalId))
        {
            logger.LogWarning("OIDC redirect: no subject claim in token");
            return Unauthorized();
        }

        var externalUser = new OidcExternalUser(externalId);
        logger.LogInformation("OIDC redirect: authorizing user {ExternalId}", externalId);
        await externalAuth.AuthorizeAsync(HttpContext, externalUser, true, Provider);

        var nextRaw = HttpContext.Request.Query["next"].ToString();
        var next = SanitizeHelper.SanitizeLocalPath(nextRaw) ?? _options.DefaultRedirectPath;

        logger.LogInformation("OIDC redirect: success, redirecting to {Next}", next);
        return Redirect(_domain + next);
    }

    private string? ExtractToken()
    {
        return OidcTokenExtractor.Extract(Request, _options);
    }
}
