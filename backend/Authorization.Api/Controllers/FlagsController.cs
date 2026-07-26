using System.Threading.Tasks;
using Authentication;
using Authorization.Api;
using Authorization.Api.Models;
using Authorization.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authorization.Api.Controllers;

[ApiController]
public sealed class FlagsController(AdminAccessService adminAccessService) : ControllerBase
{
    [JwtAuth]
    [HttpGet("v1/flags")]
    [ProducesResponseType(typeof(FlagsView), 200)]
    public async Task<IActionResult> Get()
    {
        var identity = new UserIdentity(User);
        var betaTest = await adminAccessService.HasAccessAsync(identity.Id);
        return Ok(new FlagsView(betaTest));
    }
}
