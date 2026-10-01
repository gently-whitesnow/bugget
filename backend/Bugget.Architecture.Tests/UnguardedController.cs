using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Architecture.Tests.AccessSurfaceFixtures;

/// <summary>
/// Фикстура для доказательства красноты правила поверхности доступа: контроллер вне
/// модуля reports, который не объявляет авторизацию ничем. Ровно так выглядел
/// OIDC-callback, когда конвенция ещё закрывала его по сборке.
/// </summary>
public sealed class UnguardedController : ControllerBase
{
    [HttpGet("/architecture-tests/unguarded")]
    public IActionResult Get() => Ok();
}
