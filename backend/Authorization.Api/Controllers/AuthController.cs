using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Authentication;
using Authorization.Api.Contracts.Generated;
using Authorization.Api.Generated;
using Authorization.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Authorization.Api.Controllers;

/// <summary>
/// Кто я и выход. Маршруты и формы ответов приходят из
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
    /// Информация об авторизированном пользователе.
    /// В будущем будет возвращаться только краткая информация.
    /// </summary>
    [JwtAuth]
    public override Task<ActionResult<AuthUser>> GetCurrentUser(CancellationToken cancellationToken = default)
    {
        var identity = new UserIdentity(User);
        var user = new AuthUser
        {
            Id = identity.Id.ToString(),
            Team_id = identity.TeamId,
            Workspace_id = identity.WorkspaceId,
            Workspace_role = identity.WorkspaceRole,
        };

        return Task.FromResult<ActionResult<AuthUser>>(Ok(user));
    }

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
