using Bugget.BO.Services.Internal;
using Bugget.Entities.DTO.Internal;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Controllers.Internal;

/// <summary>
/// Consumer API для <c>domain_events</c> outbox (TECHSPEC §5.2,
/// ADR-20260423-beta-bot-domain-events-outbox).
/// Auth — <c>X-Client-Name</c>; cursor — client-side, at-least-once semantics.
/// </summary>
[Route("/v2/_internal/domain-events")]
public sealed class InternalDomainEventsController(InternalDomainEventsService service) : ApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(InternalDomainEventsListResponseDto), 200)]
    public Task<IActionResult> ListAsync(
        [FromQuery(Name = "workspaceId")] string? workspaceId,
        [FromQuery(Name = "sinceId")] long? sinceId,
        [FromQuery(Name = "limit")] int? limit,
        [FromQuery(Name = "eventTypes")] string? eventTypes,
        CancellationToken ct)
        => service.ListAsync(workspaceId, sinceId, limit, eventTypes, ct).AsActionResultAsync(200);

    [HttpGet("latest-id")]
    [ProducesResponseType(typeof(InternalDomainEventLatestIdResponseDto), 200)]
    public Task<IActionResult> GetLatestIdAsync(
        [FromQuery(Name = "workspaceId")] string? workspaceId,
        CancellationToken ct)
        => service.GetLatestIdAsync(workspaceId, ct).AsActionResultAsync(200);
}
