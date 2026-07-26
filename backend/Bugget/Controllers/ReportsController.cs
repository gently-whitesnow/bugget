using System.ComponentModel.DataAnnotations;
using Bugget.BO.Mappers;
using Bugget.BO.Services.Analytics;
using Bugget.BO.Services.Reports;
using Bugget.Entities.Authentication;
using Bugget.Entities.DbModels.Report;
using Bugget.Entities.DTO.Report;
using Bugget.Entities.Options;
using Bugget.Entities.Views;
using Bugget.Entities.Views.Reports;
using Bugget.Extensions;
using Bugget.Mappers;
using Bugget.Reports.Contracts.Generated;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bugget.Controllers;

/// <summary>
/// v2 Api для работы с репортами
/// </summary>
[Route("/v2/reports")]
public sealed class ReportsController(
    ReportsService reportsService,
    AnalyticsService analyticsService,
    IOptions<ReportAliasOptions> reportAliasOptions) : ApiController
{
    /// <summary>
    /// Создать репорт
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReportSummaryViewModel), 201)]
    public async Task<ReportSummaryViewModel> CreateReportAsync([FromBody] ReportCreateDto createDto)
    {
        var user = User.GetIdentity();
        var reportSummaryDbModel = await reportsService.CreateReportAsync(user.Id, user.TeamId, user.OrganizationId, createDto);
        return reportSummaryDbModel.ToViewModel(reportAliasOptions.Value);
    }

    /// <summary>   
    /// Получить репорт
    /// </summary>
    /// <param name="aliasId">alias идентификатор репорта</param>
    /// <returns></returns>
    [HttpGet("{aliasId}")]
    [ProducesResponseType(typeof(ReportViewModel), 200)]
    public Task<IActionResult> GetReportAsync([FromRoute] string aliasId)
    {
        var user = User.GetIdentity();
        return reportsService.GetReportAsync(aliasId, user.OrganizationId, user.TeamId)
        .AsActionResultAsync(dbModel => dbModel.ToViewModel(reportAliasOptions.Value));
    }

    /// <summary>
    /// Частичное обновление репорта
    /// </summary>
    [HttpPatch("{aliasId}")]
    [ProducesResponseType(typeof(ReportPatchResultViewModel), 200)]
    public Task<IActionResult> PatchReportAsync([FromRoute] string aliasId, [FromBody] ReportPatchDto patchDto)
    {
        var user = User.GetIdentity();
        return reportsService.PatchReportAsync(aliasId, user, patchDto)
        .AsActionResultAsync(result => result.ToPatchResultViewModel(reportAliasOptions.Value));
    }

    /// <summary>
    /// Получить репорты (для главной страницы)
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [ProducesResponseType(typeof(ReportViews), 200)]
    public async Task<ReportViews> ListReportsAsync(
        [FromQuery] string? userId = null,
         [FromQuery] string? teamId = null,
         [FromQuery] int[]? reportStatuses = null,
         [FromQuery] int[]? creatorTypes = null,
         [FromQuery][Range(0, int.MaxValue)] int skip = 0,
         [FromQuery][Range(1, 100)] int take = 10)
    {
        var user = User.GetIdentity();

        var (total, reports) = await reportsService.ListReportsAsync(user.OrganizationId, userId, teamId, reportStatuses, creatorTypes, skip, take);
        return new ReportViews
        {
            Total = total,
            Reports = reports.ToViewModel(reportAliasOptions.Value)
        };
    }

    /// <summary>
    /// Детальная фазовая аналитика по конкретному репорту (sub-resource).
    /// Контракт описан в <c>bugget/specs/contracts/reports/openapi.yaml</c>;
    /// shape ответа — <see cref="AnalyticsReport"/> из <c>Bugget.Reports.Contracts</c>.
    /// </summary>
    [HttpGet("{id:long}/analytics")]
    [ProducesResponseType(typeof(AnalyticsReport), 200)]
    public async Task<ActionResult<AnalyticsReport>> GetReportAnalyticsAsync(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        if (string.IsNullOrEmpty(user.OrganizationId))
        {
            return Unauthorized();
        }

        var bo = await analyticsService.GetReportAsync(user.OrganizationId, id, cancellationToken);
        if (bo is null)
        {
            return NotFound();
        }

        return Ok(bo.ToContract());
    }

    /// <summary>
    /// Разрешить legacy reportId и вернуть teamId + teamReportId для редиректа.
    /// </summary>
    [HttpGet("legacy/{legacyId:int}")]
    [ProducesResponseType(typeof(LegacyReportResolveView), 200)]
    public async Task<IActionResult> ResolveLegacyReportAsync([FromRoute] int legacyId)
    {
        var user = User.GetIdentity();
        var resolvedReport = await reportsService.ResolveReportIdAsync(
            user.OrganizationId,
            teamId: null,
            reportId: legacyId,
            publicId: null,
            teamReportId: null
        );

        if (resolvedReport == null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(resolvedReport.CreatorTeamId) || resolvedReport.TeamReportId == null)
        {
            return NotFound();
        }

        return Ok(new LegacyReportResolveView
        {
            TeamId = resolvedReport.CreatorTeamId,
            TeamReportId = resolvedReport.TeamReportId.Value
        });
    }
}
