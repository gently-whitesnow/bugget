using System;
using System.Threading.Tasks;
using Authorization.Abstractions;
using Authorization.Api.Models.Admin;
using Authorization.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Authorization.Api.Controllers;

[Route("_internal/auth")]
[ApiController]
public sealed class InternalAdminController(IExternalAuthService externalAuthService, IRedirectService redirectService) : ControllerBase
{
    /// <summary>
    /// Метод авторизации через админку пользователей
    /// </summary>
    /// <param name="authenticateDto"></param>
    /// <returns></returns>
    [HttpPost("authenticate")]
    [ProducesResponseType(302)]
    public async Task<IActionResult> Authenticate([FromBody] AuthenticateDto authenticateDto)
    {
        await externalAuthService.AuthorizeAsync(HttpContext, authenticateDto);

        var redirectUrl = redirectService.GetRedirectUrl();

        return Redirect(redirectUrl);
    }
}
