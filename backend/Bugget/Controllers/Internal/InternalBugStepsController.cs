using Bugget.BO.Errors;
using Bugget.BO.Services.Internal;
using Bugget.Entities.DTO.Internal;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Controllers.Internal;

/// <summary>
/// POST /v2/_internal/bugs/{bugId}/steps — создание одного шага воспроизведения от имени
/// внешнего автора (TgBetaTester) с idempotency по `Idempotency-Key`. Auth — `X-Client-Name`
/// (scheme InternalClient). См. TECHSPEC §4.3.1.bis (steps endpoint), ADR-20260426.
/// </summary>
[Route("/v2/_internal/bugs/{bugId:int}/steps")]
public sealed class InternalBugStepsController(InternalBugStepsService service) : ApiController
{
    [HttpPost]
    [ProducesResponseType(typeof(InternalCreateBugStepResponseDto), 201)]
    public Task<IActionResult> CreateAsync(
        [FromRoute] int bugId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] InternalCreateBugStepRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Task.FromResult<IActionResult>(
                new JsonResult(BoErrors.IdempotencyKeyRequired)
                {
                    StatusCode = 400,
                });
        }

        return service.CreateAsync(idempotencyKey, bugId, request, ct).AsActionResultAsync(201);
    }
}
