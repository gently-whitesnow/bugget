using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Authentication;
using Authorization.Api;
using Authorization.Api.Models;
using Authorization.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Authorization.Api.Controllers;

[ApiController]
public class AuthController(
    IRefreshRevocationStore revocation,
    ILogger<AuthController> logger,
    IRedirectService redirectService
    ) : ControllerBase
{
    /// <summary>
    /// Информация об авторизированном пользователе
    /// В будущем будет возвращаться только краткая информация
    /// </summary>
    /// <returns></returns>
    [JwtAuth]
    [HttpGet("v1/auth")]
    [ProducesResponseType(typeof(AuthUserView), 200)]
    public IActionResult Me()
    {
        var identity = new UserIdentity(User);
        return Ok(new AuthUserView(identity.Id.ToString(), identity.TeamId, identity.WorkspaceId, identity.WorkspaceRole));
    }

    /// <summary>
    /// Метод разлогина
    /// </summary>
    /// <returns></returns>
    [JwtAuth]
    [HttpPost("v1/logout")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> Logout()
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

        var redirectUrl = redirectService.GetRedirectUrl();

        // Возвращаем 200 OK с URL для редиректа вместо HTTP редиректа
        return Ok(new { redirectUrl });
    }
}
