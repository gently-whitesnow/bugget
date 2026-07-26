using Bugget.BO.Services.Internal;
using Bugget.Entities.DTO.Internal;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Controllers.Internal;

/// <summary>
/// GET /v2/_internal/reports — список репортов конкретного тестера (для команды
/// <c>/my</c>). Query: <c>workspaceId</c>, <c>creatorUserId</c>, <c>limit</c>.
/// Фильтрация I-11 — только Bug, созданный этим тестером; team-added Bug'и не видны.
/// См. TECHSPEC §4.3.5.
/// </summary>
[Route("/v2/_internal/reports")]
public sealed class InternalReportsController(InternalReportsService service) : ApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(InternalReportsListResponseDto), 200)]
    public Task<IActionResult> ListAsync(
        [FromQuery] string? workspaceId,
        [FromQuery] string? creatorUserId,
        [FromQuery] int? limit,
        CancellationToken ct)
        => service.ListAsync(workspaceId, creatorUserId, limit, ct).AsActionResultAsync(200);
}
