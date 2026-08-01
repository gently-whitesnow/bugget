using Bugget.Application.Commands.Bug;
using Bugget.Application.Ports;
using Bugget.Application.Services.Comments;
using Bugget.Domain.Authentication;
using Bugget.Domain.Bugs;
using Bugget.Domain.Reports;

namespace Bugget.Application.Services.Bugs;

public class BugEventsService(
        IReportPageHubClient reportPageHubClient,
        ParticipantsService participantsService,
        CommentLogsService commentLogsService)
{
    public async Task HandleCreateBugEventAsync(ReportIdContext reportIdContext, UserIdentity user, BugSummary result)
    {
        await Task.WhenAll(
            reportPageHubClient.SendBugCreateAsync(reportIdContext.GroupKey, result, user.SignalRConnectionId),
            participantsService.AddParticipantIfNotExistAsync(reportIdContext, user.Id)
        );
    }

    public async Task HandlePatchBugEventAsync(ReportIdContext reportIdContext, int bugId, UserIdentity user, BugPatchDto patchDto)
    {
        await Task.WhenAll(
            reportPageHubClient.SendBugPatchAsync(reportIdContext.GroupKey, bugId, patchDto, user.SignalRConnectionId),
            participantsService.AddParticipantIfNotExistAsync(reportIdContext, user.Id),
            commentLogsService.LogPatchBugAsync(reportIdContext, bugId, user, patchDto)
        );
    }
}
