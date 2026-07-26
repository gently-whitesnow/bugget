using Bugget.Authentication;
using Bugget.BO.Mappers;
using Bugget.BO.Services.Reports;
using Bugget.Entities.Authentication;
using Bugget.Entities.Options;
using Bugget.Entities.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bugget.Controllers;

/// <summary>
/// Api для работы с репортами
/// </summary>
[Route("/v1/reports")]
public sealed class SearchController(
    ReportsService reportsService,
    IOptions<ReportAliasOptions> reportAliasOptions) : ApiController
{
    /// <summary>
    /// Поиск по репортам
    /// </summary>
    /// <returns></returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ReportViews), 200)]
    public async Task<ReportViews> SearchReportsAsync(
        [FromQuery] string? query,
        [FromQuery] int[]? reportStatuses,
        [FromQuery] string? userId,
        [FromQuery] string? teamId,
        [FromQuery] string? sort,
        [FromQuery] uint skip = 0,
        [FromQuery] uint take = 10,
        [FromQuery] short[]? creatorTypes = null
        )
    {
        var user = User.GetIdentity();
        var (total, reports) = await reportsService.SearchReportsAsync(
            ReportMapper.ToSearchReports(
                query,
                reportStatuses,
                userId,
                teamId,
                user.OrganizationId,
                sort,
                skip,
                take,
                creatorTypes
                ));

        return new ReportViews
        {
            Total = total,
            Reports = reports.ToViewModel(reportAliasOptions.Value)
        };
    }
}
