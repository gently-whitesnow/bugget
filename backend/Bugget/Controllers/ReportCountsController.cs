using Bugget.Api.Generated.Reports;
using Bugget.BO.Services.Reports;
using Bugget.Entities.Authentication;
using Bugget.Entities.DTO.Report;
using Bugget.Mappers;
using Bugget.Reports.Contracts.Generated;
using Bugget.Http;
using HttpProblemDetailsFactory = Bugget.Http.ProblemDetailsFactory;
using Microsoft.AspNetCore.Mvc;

namespace Bugget.Controllers;

/// <summary>
/// POST /v2/reports/counts:batch — батч-счётчики reports per scope.
/// organizationId всегда из identity (cross-tenant guard); teamId/creatorTypes/statuses — из payload.
/// Маршрут, тело запроса и форма ответа приходят из <c>specs/contracts/reports/openapi.yaml</c>.
/// </summary>
[ApiController]
public sealed class ReportCountsController(ReportsService reportsService) : ReportCountsControllerBase
{
    private const int MaxScopes = 50;

    public override async Task<ActionResult<ReportCountsBatchResponse>> CountReportsBatch(
        ReportCountsBatchRequest body,
        CancellationToken cancellationToken = default)
    {
        if (body?.Scopes is null)
        {
            return HttpProblemDetailsFactory.Create(HttpContext, ProblemDescriptors.ScopesRequired);
        }

        if (body.Scopes.Count == 0)
        {
            return Ok(new Dictionary<string, long>().ToCountsContract());
        }

        if (body.Scopes.Count > MaxScopes)
        {
            return HttpProblemDetailsFactory.Create(HttpContext,
                ProblemDescriptors.ScopesLimitExceeded,
                extensions: new Dictionary<string, object?> { ["limit"] = MaxScopes });
        }

        var seenKeys = new HashSet<string>(body.Scopes.Count);
        foreach (var scope in body.Scopes)
        {
            if (string.IsNullOrEmpty(scope.Key))
            {
                return HttpProblemDetailsFactory.Create(HttpContext, ProblemDescriptors.ScopeKeyRequired);
            }

            if (!seenKeys.Add(scope.Key))
            {
                return HttpProblemDetailsFactory.Create(HttpContext,
                    ProblemDescriptors.DuplicateScopeKey,
                    extensions: new Dictionary<string, object?> { ["key"] = scope.Key });
            }
        }

        var organizationId = User.GetIdentity().OrganizationId;

        var scopes = body.Scopes.Select(ToDto).ToArray();
        var counts = await reportsService.CountReportsBatchAsync(organizationId, scopes, cancellationToken);

        var dict = new Dictionary<string, long>(scopes.Length);
        for (var i = 0; i < scopes.Length; i++)
        {
            dict[scopes[i].Key] = counts[i];
        }

        return Ok(dict.ToCountsContract());
    }

    private static ReportCountsScopeDto ToDto(ReportCountsScope scope) => new()
    {
        Key = scope.Key,
        Statuses = scope.Statuses?.ToArray(),
        TeamId = scope.Team_id,
        CreatorTypes = scope.Creator_types?.Select(type => (short)type).ToArray()
    };
}
