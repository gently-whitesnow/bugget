using Bugget.BO.Services.Attachments;
using Bugget.DA.WebSockets;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DbModels.BugSteps;

namespace Bugget.BO.Services.Bugs;

public sealed class BugStepEventsService(
    IReportPageHubClient reportPageHubClient,
    AttachmentService attachmentService,
    ParticipantsService participantsService)
{
    public async Task HandleCreateBugStepEventAsync(ReportIdContext reportIdContext, UserIdentity user, BugStepSummaryDbModel bugStepSummaryDbModel)
    {
        await Task.WhenAll(
            reportPageHubClient.SendBugStepCreateAsync(reportIdContext.GroupKey, bugStepSummaryDbModel, user.SignalRConnectionId),
            participantsService.AddParticipantIfNotExistAsync(reportIdContext, user.Id)
        );
    }

    public async Task HandlePatchBugStepEventAsync(ReportIdContext reportIdContext, int bugId, UserIdentity user, BugStepSummaryDbModel bugStepSummaryDbModel)
    {
        await Task.WhenAll(
            reportPageHubClient.SendBugStepPatchAsync(reportIdContext.GroupKey, bugId, bugStepSummaryDbModel, user.SignalRConnectionId),
            participantsService.AddParticipantIfNotExistAsync(reportIdContext, user.Id)
        );
    }

    public async Task HandleUpdateBugStepsOrderEventAsync(ReportIdContext reportIdContext, int bugId, UserIdentity user, BugStepSummaryDbModel[] bugStepSummaryDbModels)
    {
        await Task.WhenAll(
            reportPageHubClient.SendBugStepsOrderUpdateAsync(reportIdContext.GroupKey, bugId, bugStepSummaryDbModels, user.SignalRConnectionId)
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
