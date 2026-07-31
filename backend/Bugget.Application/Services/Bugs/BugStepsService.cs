using Bugget.Application.Errors;
using Bugget.Application.Options;
using Bugget.Application.Ports;
using Bugget.Application.Services.Reports;
using Bugget.Contracts.Dto.BugStep;
using Bugget.Domain.Authentication;
using Bugget.Domain.Bugs;
using Bugget.Domain.Errors;
using Bugget.Domain.Reports;
using Microsoft.Extensions.Options;

namespace Bugget.Application.Services.Bugs;

public sealed class BugStepsService(
    IBugStepsDbClient bugStepsDbClient,
    BugStepEventsService bugStepEventsService,
    IOptions<ReportAliasOptions> aliasOptions,
    BugsService bugsService,
    ReportsService reportsService,
    ITaskQueue taskQueue)
{
    public async Task<(BugStepSummary? Value, Error? Error)> CreateBugStepAsync(UserIdentity user, string aliasId, int bugId, BugStepDto createDto)
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

        var bug = await bugsService.GetBugAsync(resolvedReport.Id, bugId);
        if (bug == null)
        {
            return (null, BoErrors.BugNotFoundError);
        }

        var bugStep = await bugStepsDbClient.CreateBugStepAsync(user.Id, bugId, createDto);

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(() => bugStepEventsService.HandleCreateBugStepEventAsync(reportIdContext, user, bugStep));

        return (bugStep, null);
    }

    public async Task<Error?> DeleteBugStepAsync(UserIdentity user, string aliasId, int bugId, int stepId)
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

        var deletedBugStep = await bugStepsDbClient.DeleteBugStepInternalAsync(resolvedReport.Id, bugId, stepId);
        if (deletedBugStep == null)
        {
            return null;
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await bugStepEventsService.HandleDeleteBugStepEventAsync(reportIdContext, bugId, user, stepId);
        return null;
    }

    public async Task<(BugStepSummary? Value, Error? Error)> PatchBugStepAsync(UserIdentity user, string aliasId, int bugId, int stepId, BugStepDto patchDto)
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

        var bugStep = await bugStepsDbClient.PatchBugStepInternalAsync(resolvedReport.Id, bugId, stepId, patchDto);
        if (bugStep == null)
        {
            return (null, BoErrors.BugStepNotFoundError);
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await bugStepEventsService.HandlePatchBugStepEventAsync(reportIdContext, bugId, user, bugStep);
        return (bugStep, null);
    }

    public async Task<(BugStepSummary[]? Value, Error? Error)> UpdateBugStepsOrderAsync(UserIdentity user, string aliasId, int bugId, BugStepsOrderDto orderDto)
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

        var bugSteps = await bugStepsDbClient.ListBugStepsInternalAsync(resolvedReport.Id, bugId);
        if (bugSteps.Length == 0)
        {
            return (null, BoErrors.BugStepsNotFoundError);
        }

        if (bugSteps.Length != orderDto.StepIds.Length)
        {
            return (null, BoErrors.BugStepsOrderSizeMismatchError);
        }

        var resultBugSteps = await bugStepsDbClient.UpdateBugStepsOrderInternalAsync(resolvedReport.Id, bugId, orderDto);

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await bugStepEventsService.HandleUpdateBugStepsOrderEventAsync(reportIdContext, bugId, user, resultBugSteps);
        return (resultBugSteps, null);
    }
}
