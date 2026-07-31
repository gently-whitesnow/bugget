using Bugget.BO.Ports;
using Bugget.BO.Services.Attachments;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.Bugs;
using Bugget.Entities.BO.ReportBo;

namespace Bugget.BO.Services.Bugs;

public sealed class BugStepEventsService(
    IReportPageHubClient reportPageHubClient,
    AttachmentService attachmentService,
    ParticipantsService participantsService)
{
    public async Task HandleCreateBugStepEventAsync(ReportIdContext reportIdContext, UserIdentity user, BugStepSummary bugStepSummary)
    {
        await Task.WhenAll(
            reportPageHubClient.SendBugStepCreateAsync(reportIdContext.GroupKey, bugStepSummary, user.SignalRConnectionId),
            participantsService.AddParticipantIfNotExistAsync(reportIdContext, user.Id)
        );
    }

    public async Task HandlePatchBugStepEventAsync(ReportIdContext reportIdContext, int bugId, UserIdentity user, BugStepSummary bugStepSummary)
    {
        await Task.WhenAll(
            reportPageHubClient.SendBugStepPatchAsync(reportIdContext.GroupKey, bugId, bugStepSummary, user.SignalRConnectionId),
            participantsService.AddParticipantIfNotExistAsync(reportIdContext, user.Id)
        );
    }

    public async Task HandleUpdateBugStepsOrderEventAsync(ReportIdContext reportIdContext, int bugId, UserIdentity user, BugStepSummary[] bugStepSummaries)
    {
        await Task.WhenAll(
            reportPageHubClient.SendBugStepsOrderUpdateAsync(reportIdContext.GroupKey, bugId, bugStepSummaries, user.SignalRConnectionId)
        );
    }

    public async Task HandleDeleteBugStepEventAsync(ReportIdContext reportIdContext, int bugId, UserIdentity user, int stepId)
    {
        await Task.WhenAll(
            reportPageHubClient.SendBugStepDeleteAsync(reportIdContext.GroupKey, bugId, stepId, user.SignalRConnectionId),
            attachmentService.DeleteBugStepAttachmentsInternalAsync(stepId)
        );
    }
}
