using System.ComponentModel.DataAnnotations;
using Bugget.Api.Extensions;
using Bugget.Api.Generated.Reports;
using Bugget.Api.Http;
using Bugget.Api.Mappers;
using Bugget.Application.Commands.Report;
using Bugget.Application.Mappers;
using Bugget.Application.Options;
using Bugget.Application.Results;
using Bugget.Application.Results.Reports;
using Bugget.Application.Services.Analytics;
using Bugget.Application.Services.Reports;
using Bugget.Contracts.Reports.Generated;
using Bugget.Domain.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bugget.Api.Controllers;

/// <summary>v2 Api репортов; маршруты, валидация тела и типы ответов — из <c>specs/contracts/reports/openapi.yaml</c>.</summary>
[ApiController]
public sealed class ReportsController(
    IReportsService reportsService,
    IAnalyticsService analyticsService,
    IOptions<ReportAliasOptions> reportAliasOptions) : ReportsControllerBase
{
    public override async Task<ActionResult<ReportSummary>> CreateReport(
        ReportCreateRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var reportSummary = await reportsService.CreateReportAsync(
            user,
            new ReportCreateDto { Title = body.Title });

        var contract = reportSummary.ToViewModel(reportAliasOptions.Value).ToContract();
        var location = $"/api/app/workspaces/{user.OrganizationId}/teams/{user.TeamId}/v2/reports/{contract.Id}";

        return Created(location, contract);
    }

    public override Task<ActionResult<Report>> GetReport(
        string aliasId,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        return reportsService.GetReportAsync(aliasId, user.OrganizationId, user.TeamId)
            .AsContractResultAsync(HttpContext, dbModel => dbModel.ToViewModel(reportAliasOptions.Value).ToContract());
    }

    public override Task<ActionResult<ReportPatchResult>> PatchReport(
        string aliasId,
        ReportPatchRequest body,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        var patchDto = new ReportPatchDto
        {
            Title = body.Title,
            Status = body.Status?.ToDomainValue(),
            ResponsibleUserId = body.Responsible_user_id,
            IsExcludedFromAnalytics = body.Is_excluded_from_analytics
        };

        return reportsService.PatchReportAsync(aliasId, user, patchDto)
            .AsContractResultAsync(HttpContext, result => result.ToPatchResultViewModel(reportAliasOptions.Value).ToContract());
    }

    // skip/take ограничены здесь, а не в базе: NSwag не переносит minimum/maximum query-параметров в атрибуты.
    public override async Task<ActionResult<ReportList>> ListReports(
        string? userId = null,
        string? teamId = null,
        IEnumerable<ReportStatus>? reportStatuses = null,
        IEnumerable<CreatorType>? creatorTypes = null,
        [Range(0, int.MaxValue)] int? skip = 0,
        [Range(1, 100)] int? take = 10,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();

        var (total, reports) = await reportsService.ListReportsAsync(
            user.OrganizationId,
            userId,
            teamId,
            reportStatuses?.Select(status => status.ToDomainValue()).ToArray(),
            creatorTypes?.Select(type => type.ToDomainValue()).ToArray(),
            skip ?? 0,
            take ?? 10);

        return new ReportViews
        {
            Total = total,
            Reports = reports.ToViewModel(reportAliasOptions.Value)
        }.ToContract();
    }

    /// <remarks>
    /// Сегмент — строка канонического Int64 (shared.yaml <c>Int64String</c>), конверсия в <c>long</c> живёт здесь.
    /// Ограничение <c>:long</c> оставлено: нечисловой сегмент по-прежнему 404, а неканоничный
    /// (<c>-5</c>, <c>007</c>) до сервиса не доезжает — <see cref="WireInt64"/> отвечает 400.
    /// </remarks>
    [RouteParameterConstraint("id", "long")]
    public override async Task<ActionResult<AnalyticsReport>> GetReportAnalytics(
        string id,
        CancellationToken cancellationToken = default)
    {
        var user = User.GetIdentity();
        if (string.IsNullOrEmpty(user.OrganizationId))
        {
            return Unauthorized();
        }

        var invalidId = WireInt64.TryBindRouteValue(HttpContext, "id", id, out var reportId);
        if (invalidId is not null)
        {
            return invalidId;
        }

        var bo = await analyticsService.GetReportAsync(user.OrganizationId, reportId, cancellationToken);
        if (bo is null)
        {
            return NotFound();
        }

        return Ok(bo.ToContract());
    }

    /// <summary>Разрешить legacy reportId и вернуть teamId + teamReportId для редиректа.</summary>
    [RouteParameterConstraint("legacyId", "int")]
    public override async Task<ActionResult<LegacyReportResolve>> ResolveLegacyReport(
        int legacyId,
        CancellationToken cancellationToken = default)
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
        }.ToContract());
    }
}
