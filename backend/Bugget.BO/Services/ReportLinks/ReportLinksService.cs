using Bugget.BO.Errors;
using Bugget.BO.Ports;
using Bugget.BO.Services.Reports;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DTO.Link;
using Bugget.Entities.Errors;
using Bugget.Entities.Options;
using Microsoft.Extensions.Options;
using TaskQueue;

namespace Bugget.BO.Services.ReportLinks;

public sealed class ReportLinksService(
    IReportLinksDbClient reportLinksDbClient,
    ReportLinkEventsService reportLinkEventsService,
    ITaskQueue taskQueue,
    ReportsService reportsService,
    IOptions<ReportAliasOptions> aliasOptions)
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

        var linkDbModel = await reportLinksDbClient.CreateReportLinkInternalAsync(resolvedReport.Id, dto);

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () => await reportLinkEventsService.HandleReportLinkCreateAsync(reportIdContext, user, linkDbModel));

        return (linkDbModel, null);
    }

    public async Task<(ReportLink? Value, Error? Error)> CreateReportLinkInternalAsync(UserIdentity user, ReportIdContext reportIdContext, ReportLinkDto dto)
    {
        var linkDbModel = await reportLinksDbClient.CreateReportLinkInternalAsync(reportIdContext.ReportId, dto);
        await taskQueue.EnqueueAsync(async () => await reportLinkEventsService.HandleReportLinkCreateAsync(reportIdContext, user, linkDbModel));
        return (linkDbModel, null);
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

        var reportLinkDbModel = await reportLinksDbClient.DeleteReportLinkInternalAsync(resolvedReport.Id, linkId);
        if (reportLinkDbModel == null)
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

        var linkDbModel = await reportLinksDbClient.UpdateReportLinkInternalAsync(resolvedReport.Id, linkId, dto);
        if (linkDbModel == null)
        {
            return (null, BoErrors.ReportLinkNotFound);
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () => await reportLinkEventsService.HandleReportLinkUpdateAsync(reportIdContext, user, linkDbModel));

        return (linkDbModel, null);
    }
}
