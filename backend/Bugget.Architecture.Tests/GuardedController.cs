using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Architecture.Tests.AccessSurfaceFixtures;

/// <summary>
/// Фикстура для доказательства зелёности: тот же контроллер, но с собственным атрибутом
/// авторизации. Правило смотрит на объявленный доступ, а не на имя или расположение типа.
/// </summary>
[Authorize]
public sealed class GuardedController : ControllerBase
{
    [HttpGet("/architecture-tests/guarded")]
    public IActionResult Get() => Ok();
}
