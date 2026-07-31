using Bugget.Api.Authorization.Abstractions;
using Bugget.Api.Authorization.Fake.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugget.Api.Authorization.Fake;

[ApiController, Route("v1/fake")]
public sealed class FakeController(
    IExternalAuthService externalAuth,
    IOptions<FakeAuthOptions> options,
    ILogger<FakeController> logger) : ControllerBase
{
    private readonly FakeAuthOptions _options = options.Value;
    private readonly string _domain = Environment.GetEnvironmentVariable("APP_DOMAIN")
        ?? throw new InvalidOperationException("APP_DOMAIN is not set");

    /// <summary>
    /// Fake login endpoint for local development.
    /// Accepts user data via query parameters and authorizes without any validation.
    /// </summary>
    /// <param name="externalId">External user identifier (required)</param>
    /// <param name="name">User display name (optional)</param>
    /// <param name="imageUrl">User avatar URL (optional)</param>
    /// <param name="next">Redirect path after authorization (optional)</param>
    [HttpGet("login")]
    public async Task<IActionResult> LoginAsync(
        [FromQuery] string externalId,
        [FromQuery] string? name = null,
        [FromQuery] string? imageUrl = null,
        [FromQuery] string? next = null)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return BadRequest();
        }

        var externalUser = new FakeExternalUser(externalId, name, imageUrl);

        logger.LogWarning(
            "FAKE AUTH: Authorizing user {ExternalId} ({Name}) - THIS SHOULD ONLY BE USED IN DEVELOPMENT",
            externalId,
            name ?? "no name");

        // MAIN-20: контракт помечен [Obsolete] намеренно (миграция на provider-required API —
        // отдельная задача). До слияния проектов предупреждение было не видно: у модулей
        // authorization/oidc был выключен TreatWarningsAsErrors. Функциональных правок в PR
        // слияния нет, поэтому вызов оставлен как есть под точечным подавлением.
#pragma warning disable CS0618
        await externalAuth.AuthorizeAsync(HttpContext, externalUser);
#pragma warning restore CS0618

        var redirectPath = SanitizeHelper.SanitizeLocalPath(next) ?? _options.DefaultRedirectPath;

        logger.LogInformation("FAKE AUTH: Success, redirecting to {Next}", redirectPath);
        return Redirect(_domain + redirectPath);
    }
}
