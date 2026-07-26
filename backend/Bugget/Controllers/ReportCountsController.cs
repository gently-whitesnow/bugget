using Bugget.Authentication;
using Bugget.BO.Services.Reports;
using Bugget.Entities.Authentication;
using Bugget.Entities.DTO.Report;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Controllers;

/// <summary>
/// POST /v2/reports/counts:batch — батч-счётчики reports per scope.
/// organizationId всегда из identity (cross-tenant guard); teamId/creatorTypes/statuses — из payload.
/// </summary>
[Route("/v2/reports")]
public sealed class ReportCountsController(ReportsService reportsService) : ApiController
{
    private const int MaxScopes = 50;

    [HttpPost("counts:batch")]
    [ProducesResponseType(typeof(ReportCountsBatchResponseDto), 200)]
    public async Task<IActionResult> CountAsync(
        [FromBody] ReportCountsBatchRequestDto request,
        CancellationToken ct)
    {
        if (request?.Scopes is null)
        {
            return BadRequest(new { error = "scopes_required" });
        }

        if (request.Scopes.Length == 0)
        {
            return Ok(new ReportCountsBatchResponseDto { Counts = new Dictionary<string, long>() });
        }

        if (request.Scopes.Length > MaxScopes)
        {
            return BadRequest(new { error = "scopes_limit_exceeded", limit = MaxScopes });
        }

        var seenKeys = new HashSet<string>(request.Scopes.Length);
        foreach (var scope in request.Scopes)
        {
            if (string.IsNullOrEmpty(scope.Key))
            {
                return BadRequest(new { error = "scope_key_required" });
            }

            if (!seenKeys.Add(scope.Key))
            {
                return BadRequest(new { error = "duplicate_scope_key", key = scope.Key });
            }
        }

        var organizationId = User.GetIdentity().OrganizationId;

        var counts = await reportsService.CountReportsBatchAsync(organizationId, request.Scopes, ct);

        var dict = new Dictionary<string, long>(request.Scopes.Length);
        for (var i = 0; i < request.Scopes.Length; i++)
        {
            dict[request.Scopes[i].Key] = counts[i];
        }

        return Ok(new ReportCountsBatchResponseDto { Counts = dict });
    }
}
