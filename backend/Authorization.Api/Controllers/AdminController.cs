using System;
using System.Threading.Tasks;
using Authorization.Abstractions;
using Authorization.Api.Models.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Authorization.Api.Controllers;

[Route("v1/admin")]
[ApiController]
public sealed class AdminController(IExternalAuthService externalAuthService) : ControllerBase
{
    /// <summary>
    /// Метод для авторизации в saas только в режиме разработки
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    [HttpGet("authenticate")]
    [ProducesResponseType(302)]
    public async Task<IActionResult> Authenticate([FromQuery] string name)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        var authenticateDto = new AuthenticateDto
        {
            Name = name,
            ExternalId = Guid.NewGuid().ToString(),
            ImageUrl = null
        };

        await externalAuthService.AuthorizeAsync(HttpContext, authenticateDto);

        return Redirect("/");
    }
}
