using Microsoft.AspNetCore.Mvc;

namespace Bugget.Controllers.Internal;

[Route("/v2/_internal/ping")]
public sealed class InternalPingController : ApiController
{
    [HttpGet]
    public IActionResult Get() => Ok("pong");
}
