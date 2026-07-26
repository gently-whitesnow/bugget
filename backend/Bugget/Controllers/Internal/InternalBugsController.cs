using Bugget.BO.Errors;
using Bugget.BO.Services.Internal;
using Bugget.Entities.DTO.Internal;
using Bugget.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Controllers.Internal;

/// <summary>
/// POST /v2/_internal/bugs — создание Report+Bug от имени внешнего автора (TgBetaTester)
/// с idempotency по заголовку `Idempotency-Key`. Доступен только с подтверждённым
/// `X-Client-Name` (auth scheme InternalClient). См. TECHSPEC §4.3.1.
/// </summary>
[Route("/v2/_internal/bugs")]
public sealed class InternalBugsController(
    InternalBugsService service,
    InternalBugDetailService detailService) : ApiController
{
    [HttpPost]
    [ProducesResponseType(typeof(InternalCreateBugResponseDto), 201)]
    public Task<IActionResult> CreateAsync(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] InternalCreateBugRequestDto request,
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

        return service.CreateAsync(idempotencyKey, request, ct).AsActionResultAsync(201);
    }

    /// <summary>
    /// GET /v2/_internal/bugs/{bugId} — полная карточка bug'а (TECHSPEC §4.3.6).
    /// Используется ботом в callback /my:&lt;bugId&gt; для рендера деталей.
    /// </summary>
    [HttpGet("{bugId:int}")]
    [ProducesResponseType(typeof(InternalBugDetailResponseDto), 200)]
    public Task<IActionResult> GetAsync(
        [FromRoute] int bugId,
        CancellationToken ct)
        => detailService.GetAsync(bugId, ct).AsActionResultAsync(200);
}
