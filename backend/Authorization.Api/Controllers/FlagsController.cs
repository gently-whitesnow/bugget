using System.Threading;
using System.Threading.Tasks;
using Authentication;
using Authorization.Api.Contracts.Generated;
using Authorization.Api.Generated;
using Authorization.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Authorization.Api.Controllers;

/// <summary>
/// Флаги доступа текущего пользователя. Маршрут и форма ответа приходят из
/// <c>specs/contracts/authorization/openapi.yaml</c> через <see cref="FlagsControllerBase"/>.
/// </summary>
[ApiController]
public sealed class FlagsController(AdminAccessService adminAccessService) : FlagsControllerBase
{
    [JwtAuth]
    public override async Task<ActionResult<Flags>> GetFlags(CancellationToken cancellationToken = default)
    {
        var identity = new UserIdentity(User);
        var betaTest = await adminAccessService.HasAccessAsync(identity.Id);
        return Ok(new Flags { Beta_test = betaTest });
    }
}
