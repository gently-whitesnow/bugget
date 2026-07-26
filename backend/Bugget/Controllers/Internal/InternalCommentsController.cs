using Bugget.BO.Services.Internal;
using Bugget.Entities.DTO.Internal;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Controllers.Internal;

/// <summary>
/// _internal comments write/read pair для диалога с тестером. См. TECHSPEC §4.3.3/§4.3.4.
/// Auth — <c>X-Client-Name</c> (InternalClient scheme через <see cref="Authentication.UserAuthHandler"/>).
/// </summary>
[Route("/v2/_internal/bugs/{bugId:int}")]
public sealed class InternalCommentsController(InternalCommentsService service) : ApiController
{
    [HttpPost("comments")]
    [ProducesResponseType(typeof(InternalCreateCommentResponseDto), 201)]
    public Task<IActionResult> CreateAsync(
        [FromRoute] int bugId,
        [FromBody] InternalCreateCommentRequestDto request,
        CancellationToken ct)
        => service.CreateAsync(bugId, request, ct).AsActionResultAsync(201);

    [HttpGet("external-comments")]
    [ProducesResponseType(typeof(InternalExternalCommentsResponseDto), 200)]
    public Task<IActionResult> ListExternalAsync(
        [FromRoute] int bugId,
        [FromQuery] int sinceId,
        [FromQuery] int? limit,
        CancellationToken ct)
        => service.ListExternalAsync(bugId, sinceId, limit, ct).AsActionResultAsync(200);
}
