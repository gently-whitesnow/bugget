using System.ComponentModel.DataAnnotations;
using Bugget.Api.Generated.Reports;
using Bugget.Api.Mappers;
using Bugget.Application.Mappers;
using Bugget.Application.Options;
using Bugget.Application.Results;
using Bugget.Application.Services.Reports;
using Bugget.Contracts.Reports.Generated;
using Bugget.Domain.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bugget.Api.Controllers;

/// <summary>
/// Поиск по репортам. Маршрут и форма ответа приходят из
/// <c>specs/contracts/reports/openapi.yaml</c> через <see cref="SearchControllerBase"/> —
/// ответ тот же <c>ReportList</c>, что и у списка репортов.
/// </summary>
[ApiController]
public sealed class SearchController(
    IReportsService reportsService,
    IOptions<ReportAliasOptions> reportAliasOptions) : SearchControllerBase
{
    public override async Task<ActionResult<ReportList>> SearchReports(
        string? query = null,
        IEnumerable<int>? reportStatuses = null,
        string? userId = null,
        string? teamId = null,
        string? sort = null,
        // Неотрицательность skip/take генератор в атрибуты не переносит: до
        // contract-first параметры были uint, и отрицательное значение отсекалось
        // связыванием.
        [Range(0, int.MaxValue)] int? skip = 0,
        [Range(0, int.MaxValue)] int? take = 10,
        IEnumerable<int>? creatorTypes = null,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var (total, reports) = await reportsService.SearchReportsAsync(
            ReportMapper.ToSearchReports(
                query,
                reportStatuses?.ToArray(),
                userId,
                teamId,
                user.OrganizationId,
                sort,
                (uint)(skip ?? 0),
                (uint)(take ?? 10),
                creatorTypes?.Select(type => (short)type).ToArray()
                ));

        return new ReportViews
        {
            Total = total,
            Reports = reports.ToViewModel(reportAliasOptions.Value)
        }.ToContract();
    }
}
