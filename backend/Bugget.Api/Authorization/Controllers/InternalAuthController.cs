using Microsoft.AspNetCore.Mvc;

namespace Bugget.Api.Authorization.Controllers;

[ApiController]
public class InternalAuthController : ControllerBase
{
    /// <summary>
    /// Внутренний метод для проверки авторизации пользователя (nginx proxy)
    /// </summary>
    /// <returns></returns>
    [HttpGet("/_internal/auth")]
    [InternalAuth]
    [ProducesResponseType(200)]
    public IActionResult Auth()
    {
        return Ok();
    }
}
