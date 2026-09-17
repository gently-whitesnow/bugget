using Bugget.Application.Commands.BugStep;
using Bugget.Application.Errors;
using Bugget.Application.Options;
using Bugget.Application.Ports;
using Bugget.Application.Services.Attachments;
using Bugget.Application.Services.Reports;
using Bugget.Domain;
using Bugget.Domain.Authentication;
using Bugget.Domain.Bugs;
using Bugget.Domain.Errors;
using Bugget.Domain.Reports;
using Microsoft.Extensions.Options;

namespace Bugget.Application.Services.Bugs;

public sealed class BugStepsService(
    IBugStepsDbClient bugStepsDbClient,
    BugStepEventsService bugStepEventsService,
    AttachmentBatchWriter attachmentBatchWriter,
    IOptions<ReportAliasOptions> aliasOptions,
    IBugsService bugsService,
    IReportsService reportsService,
    ITaskQueue taskQueue) : IBugStepsService
{
    public async Task<(BugStepSummary? Value, Error? Error)> CreateBugStepAsync(UserIdentity user, string aliasId, int bugId, BugStepDto createDto)
    {
        var (resolvedReport, error) = await ResolveBugReportAsync(user, aliasId, bugId);
        if (resolvedReport == null)
        {
            return (null, error);
        }

        var bugStep = await bugStepsDbClient.CreateBugStepAsync(user.Id, bugId, createDto);

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(() => bugStepEventsService.HandleCreateBugStepEventAsync(reportIdContext, user, bugStep));

        return (bugStep, null);
    }

    public async Task<(BugStepSummary? Value, Error? Error)> CreateBugStepWithAttachmentsAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        BugStepDto createDto,
        IReadOnlyList<AttachmentUpload> files,
        CancellationToken ct)
    {
        var (resolvedReport, error) = await ResolveBugReportAsync(user, aliasId, bugId);
        error ??= AttachmentBatchWriter.Validate(files);
        if (resolvedReport == null || error != null)
        {
            return (null, error);
        }

        var (bugStep, attachments) = await attachmentBatchWriter.CreateAsync(
            new AttachmentBatchTarget(user, resolvedReport.Id, AttachType.BugStep),
            files,
            async (scope, _) =>
            {
                var created = await bugStepsDbClient.CreateBugStepAsync(scope, user.Id, bugId, createDto);
                return (created, created.Id);
            },
            ct);

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async queueToken =>
        {
            await bugStepEventsService.HandleCreateBugStepEventAsync(reportIdContext, user, bugStep);
            await attachmentBatchWriter.PublishCreatedAsync(reportIdContext, user, attachments, queueToken);
        });

        bugStep.Attachments = attachments;
        return (bugStep, null);
    }

    private async Task<(ResolvedReportId? Report, Error? Error)> ResolveBugReportAsync(UserIdentity user, string aliasId, int bugId)
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
        return bug == null ? (null, BoErrors.BugNotFoundError) : (resolvedReport, null);
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
