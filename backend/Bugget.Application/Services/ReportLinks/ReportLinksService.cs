using Bugget.Application.Commands.Link;
using Bugget.Application.Errors;
using Bugget.Application.Options;
using Bugget.Application.Ports;
using Bugget.Application.Services.Reports;
using Bugget.Domain.Authentication;
using Bugget.Domain.Errors;
using Bugget.Domain.Reports;
using Microsoft.Extensions.Options;

namespace Bugget.Application.Services.ReportLinks;

public sealed class ReportLinksService(
    IReportLinksDbClient reportLinksDbClient,
    ReportLinkEventsService reportLinkEventsService,
    ITaskQueue taskQueue,
    IReportsService reportsService,
    IOptions<ReportAliasOptions> aliasOptions) : IReportLinksService
{
    public async Task<(ReportLink? Value, Error? Error)> CreateReportLinkAsync(UserIdentity user, string aliasId, ReportLinkDto dto)
    {
        var (reportId, publicId, teamReportId) = ReportIdResolveHelper.ResolveReportId(aliasId, aliasOptions.Value);
        var resolvedReport = await reportsService.ResolveReportIdAsync(
            user.OrganizationId,
            user.TeamId,
            reportId,
            publicId,
            teamReportId
        );
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var link = await reportLinksDbClient.CreateReportLinkInternalAsync(resolvedReport.Id, dto);

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () => await reportLinkEventsService.HandleReportLinkCreateAsync(reportIdContext, user, link));

        return (link, null);
    }

    public async Task<(ReportLink? Value, Error? Error)> CreateReportLinkInternalAsync(UserIdentity user, ReportIdContext reportIdContext, ReportLinkDto dto)
    {
        var link = await reportLinksDbClient.CreateReportLinkInternalAsync(reportIdContext.ReportId, dto);
        await taskQueue.EnqueueAsync(async () => await reportLinkEventsService.HandleReportLinkCreateAsync(reportIdContext, user, link));
        return (link, null);
    }

    public async Task<Error?> DeleteReportLinkAsync(UserIdentity user, string aliasId, int linkId)
    {
        var (reportId, publicId, teamReportId) = ReportIdResolveHelper.ResolveReportId(aliasId, aliasOptions.Value);
        var resolvedReport = await reportsService.ResolveReportIdAsync(
            user.OrganizationId,
            user.TeamId,
            reportId,
            publicId,
            teamReportId
        );
        if (resolvedReport == null)
        {
            return BoErrors.ReportNotFoundError;
        }

        var link = await reportLinksDbClient.DeleteReportLinkInternalAsync(resolvedReport.Id, linkId);
        if (link == null)
        {
            return null;
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () => await reportLinkEventsService.HandleReportLinkDeleteAsync(reportIdContext, user, linkId));
        return null;
    }

    public async Task<(ReportLink? Value, Error? Error)> UpdateReportLinkAsync(UserIdentity user, string aliasId, int linkId, ReportLinkDto dto)
    {
        var (reportId, publicId, teamReportId) = ReportIdResolveHelper.ResolveReportId(aliasId, aliasOptions.Value);
        var resolvedReport = await reportsService.ResolveReportIdAsync(
            user.OrganizationId,
            user.TeamId,
            reportId,
            publicId,
            teamReportId
        );
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var link = await reportLinksDbClient.UpdateReportLinkInternalAsync(resolvedReport.Id, linkId, dto);
        if (link == null)
        {
            return (null, BoErrors.ReportLinkNotFound);
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () => await reportLinkEventsService.HandleReportLinkUpdateAsync(reportIdContext, user, link));

        return (link, null);
    }
}
