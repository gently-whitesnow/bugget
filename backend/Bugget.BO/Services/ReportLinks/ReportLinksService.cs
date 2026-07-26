using Bugget.BO.Errors;
using Bugget.BO.Services.Reports;
using Bugget.DA.Interfaces;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DbModels.ReportLink;
using Bugget.Entities.DTO.Link;
using Bugget.Entities.Options;
using Microsoft.Extensions.Options;
using Monade;
using TaskQueue;

namespace Bugget.BO.Services.ReportLinks;

public sealed class ReportLinksService(
    IReportLinksDbClient reportLinksDbClient,
    ReportLinkEventsService reportLinkEventsService,
    ITaskQueue taskQueue,
    ReportsService reportsService,
    IOptions<ReportAliasOptions> aliasOptions)
{
    public async Task<MonadeStruct<ReportLinkDbModel>> CreateReportLinkAsync(UserIdentity user, string aliasId, ReportLinkDto dto)
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

        var linkDbModel = await reportLinksDbClient.CreateReportLinkInternalAsync(resolvedReport.Id, dto);

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () => await reportLinkEventsService.HandleReportLinkCreateAsync(reportIdContext, user, linkDbModel));

        return linkDbModel;
    }

    public async Task<MonadeStruct<ReportLinkDbModel>> CreateReportLinkInternalAsync(UserIdentity user, ReportIdContext reportIdContext, ReportLinkDto dto)
    {
        var linkDbModel = await reportLinksDbClient.CreateReportLinkInternalAsync(reportIdContext.ReportId, dto);
        await taskQueue.EnqueueAsync(async () => await reportLinkEventsService.HandleReportLinkCreateAsync(reportIdContext, user, linkDbModel));
        return linkDbModel;
    }

    public async Task<MonadeStruct> DeleteReportLinkAsync(UserIdentity user, string aliasId, int linkId)
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
            return MonadeStruct.Success;
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () => await reportLinkEventsService.HandleReportLinkDeleteAsync(reportIdContext, user, linkId));
        return MonadeStruct.Success;
    }

    public async Task<MonadeStruct<ReportLinkDbModel>> UpdateReportLinkAsync(UserIdentity user, string aliasId, int linkId, ReportLinkDto dto)
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

        var linkDbModel = await reportLinksDbClient.UpdateReportLinkInternalAsync(resolvedReport.Id, linkId, dto);
        if (linkDbModel == null)
        {
            return BoErrors.ReportLinkNotFound;
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () => await reportLinkEventsService.HandleReportLinkUpdateAsync(reportIdContext, user, linkDbModel));

        return linkDbModel;
    }
}
