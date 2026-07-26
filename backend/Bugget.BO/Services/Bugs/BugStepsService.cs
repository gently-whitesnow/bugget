using Bugget.BO.Errors;
using Bugget.BO.Services.Reports;
using Bugget.DA.Interfaces;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DbModels.BugSteps;
using Bugget.Entities.DTO.BugStep;
using Bugget.Entities.Options;
using Microsoft.Extensions.Options;
using Monade;
using TaskQueue;

namespace Bugget.BO.Services.Bugs;

public sealed class BugStepsService(
    IBugStepsDbClient bugStepsDbClient,
    BugStepEventsService bugStepEventsService,
    IOptions<ReportAliasOptions> aliasOptions,
    BugsService bugsService,
    ReportsService reportsService,
    ITaskQueue taskQueue)
{
    public async Task<MonadeStruct<BugStepSummaryDbModel>> CreateBugStepAsync(UserIdentity user, string aliasId, int bugId, BugStepDto createDto)
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

        var bugDbModel = await bugsService.GetBugAsync(resolvedReport.Id, bugId);
        if (bugDbModel == null)
        {
            return BoErrors.BugNotFoundError;
        }

        var bugStepDbModel = await bugStepsDbClient.CreateBugStepAsync(user.Id, bugId, createDto);

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(() => bugStepEventsService.HandleCreateBugStepEventAsync(reportIdContext, user, bugStepDbModel));

        return bugStepDbModel;
    }

    public async Task<MonadeStruct> DeleteBugStepAsync(UserIdentity user, string aliasId, int bugId, int stepId)
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

        var deletedBugStepDbModel = await bugStepsDbClient.DeleteBugStepInternalAsync(resolvedReport.Id, bugId, stepId);
        if (deletedBugStepDbModel == null)
        {
            return MonadeStruct.Success;
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await bugStepEventsService.HandleDeleteBugStepEventAsync(reportIdContext, bugId, user, stepId);
        return MonadeStruct.Success;
    }

    public async Task<MonadeStruct<BugStepSummaryDbModel>> PatchBugStepAsync(UserIdentity user, string aliasId, int bugId, int stepId, BugStepDto patchDto)
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

        var bugStepDbModel = await bugStepsDbClient.PatchBugStepInternalAsync(resolvedReport.Id, bugId, stepId, patchDto);
        if (bugStepDbModel == null)
        {
            return BoErrors.BugStepNotFoundError;
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await bugStepEventsService.HandlePatchBugStepEventAsync(reportIdContext, bugId, user, bugStepDbModel);
        return bugStepDbModel;
    }

    public async Task<MonadeStruct<BugStepSummaryDbModel[]>> UpdateBugStepsOrderAsync(UserIdentity user, string aliasId, int bugId, BugStepsOrderDto orderDto)
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

        var bugStepsDbModels = await bugStepsDbClient.ListBugStepsInternalAsync(resolvedReport.Id, bugId);
        if (bugStepsDbModels.Length == 0)
        {
            return BoErrors.BugStepsNotFoundError;
        }

        if (bugStepsDbModels.Length != orderDto.StepIds.Length)
        {
            return BoErrors.BugStepsOrderSizeMismatchError;
        }

        var resultBugStepsDbModels = await bugStepsDbClient.UpdateBugStepsOrderInternalAsync(resolvedReport.Id, bugId, orderDto);

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await bugStepEventsService.HandleUpdateBugStepsOrderEventAsync(reportIdContext, bugId, user, resultBugStepsDbModels);
        return resultBugStepsDbModels;
    }
}
