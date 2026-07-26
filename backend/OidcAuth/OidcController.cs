using Authorization.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OidcAuth.Models;

namespace OidcAuth;

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

    /// <summary>
    /// Callback после OIDC авторизации через oauth2-proxy.
    /// Валидирует токен, привязывает OIDC identity, редиректит на next.
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> CallbackAsync()
    {
        // 1. Извлекаем токен из cookie
        var token = ExtractToken();
        if (string.IsNullOrEmpty(token))
        {
            logger.LogWarning("OIDC redirect: no token found");
            return Unauthorized("No OIDC token found");
        }

        // 2. Валидируем токен
        var principal = await tokenValidator.ValidateTokenAsync(token, HttpContext.RequestAborted);
        if (principal == null)
        {
            logger.LogWarning("OIDC redirect: token validation failed");
            return Unauthorized("Invalid OIDC token");
        }

        // 3. Извлекаем external_id (sub claim)
        var externalId = tokenValidator.GetSubject(principal);
        if (string.IsNullOrEmpty(externalId))
        {
            logger.LogWarning("OIDC redirect: no subject claim in token");
            return Unauthorized("No subject in token");
        }

        // 4. Создаём external user и привязываем OIDC identity.
        var externalUser = new OidcExternalUser(externalId);
        logger.LogInformation("OIDC redirect: authorizing user {ExternalId}", externalId);
        await externalAuth.AuthorizeAsync(HttpContext, externalUser, true, Provider);

        // 5. Редирект на next (sanitized)
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
