using System;
using System.Threading.Tasks;
using Authentication;
using Authorization.Api;
using Authorization.Api.Services;
using Authorization.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Authorization.Api.Controllers;

[ApiController]
public class InternalAuthController(
    ILogger<InternalAuthController> logger,
    AdminAccessService adminAccessService
    ) : ControllerBase
{
    private const string DeviceCookieName = "device_id";
    private static readonly TimeSpan DeviceCookieLifetime = TimeSpan.FromDays(7);


    /// <summary>
    /// Внутренний метод для проверки авторизации пользователя (nginx proxy)
    /// </summary>
    /// <returns></returns>
    [HttpGet("/_internal/auth")]
    [JwtAuth]
    [ProducesResponseType(200)]
    public IActionResult Auth()
    {
        return Ok();
    }

    /// <summary>
    /// Внутренний метод для авторизации анонимного пользователя (nginx proxy)
    /// </summary>
    /// <returns></returns>
    [HttpGet("/_internal/anon/auth")]
    [ProducesResponseType(200)]
    public IActionResult AnonymousAuth()
    {
        var deviceId = EnsureDeviceIdCookie();
        HttpContext.Response.Headers["Auth-Request-User-Id"] = deviceId;

        return Ok();
    }


    private string EnsureDeviceIdCookie()
    {
        if (Request.Cookies.TryGetValue(DeviceCookieName, out var deviceId) &&
            !string.IsNullOrWhiteSpace(deviceId))
        {
            return deviceId;
        }

        var newDeviceId = Guid.NewGuid().ToString();
        var cookie = HttpContextExtensions.BuildCookieHeader(DeviceCookieName, newDeviceId, DeviceCookieLifetime);

        // Для subrequest (nginx auth_request) используем кастомный заголовок,
        // так как nginx не передает Set-Cookie из subrequest клиенту
        // /_internal/anon/auth всегда вызывается как subrequest через nginx
        HttpContext.Response.Headers["X-Auth-Set-Cookie-Device-Id"] = cookie;

        logger.LogInformation("Issued new device id cookie for anonymous user: {DeviceId}", newDeviceId);

        return newDeviceId;
    }

    /// <summary>
    /// Внутренний метод для проверки авторизации администратора (nginx proxy)
    /// </summary>
    /// <returns></returns>
    [JwtAuth]
    [HttpGet("/_internal/admin")]
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> CheckAdminAccess()
    {
        var identity = new UserIdentity(User);

        if (!await adminAccessService.HasAccessAsync(identity.Id))
        {
            return Forbid();
        }

        return Ok();
    }
}
