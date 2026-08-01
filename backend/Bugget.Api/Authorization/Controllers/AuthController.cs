using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bugget.Api.Authorization.Services;
using Bugget.Api.Generated.Authorization;
using Bugget.Api.Users.Authentication;
using Bugget.Application.Authorization;
using Bugget.Application.Authorization.Ports;
using Bugget.Contracts.Authorization.Generated;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Bugget.Api.Authorization.Controllers;

/// <summary>
/// Выход из системы. Маршрут и форма ответа приходят из
/// <c>specs/contracts/authorization/openapi.yaml</c> через <see cref="AuthControllerBase"/>.
/// </summary>
[ApiController]
public class AuthController(
    IRefreshRevocationStore revocation,
    ILogger<AuthController> logger,
    IRedirectService redirectService
    ) : AuthControllerBase
{
    /// <summary>
    /// Метод разлогина.
    /// </summary>
    [JwtAuth]
    public override async Task<ActionResult<LogoutResult>> Logout(CancellationToken cancellationToken = default)
    {
        var refresh = HttpContext.Request.Cookies["refresh_token"];
        logger.LogInformation("Logout request received. Refresh token: {RefreshToken}", refresh);
        if (!string.IsNullOrWhiteSpace(refresh))
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(refresh);
            var jti = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
            var exp = jwt.ValidTo;
            await revocation.RevokeAsync(jti, exp);
        }

        // чистим cookies
        HttpContext.Response.Cookies.Delete("access_token");
        HttpContext.Response.Cookies.Delete("refresh_token");

        // Возвращаем 200 OK с URL для редиректа вместо HTTP редиректа: решение
        // о переходе принимает фронт.
        return Ok(new LogoutResult { Redirect_url = redirectService.GetRedirectUrl() });
    }
}
