using Bugget.BO.Services.Comments;
using Bugget.DA.WebSockets;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DbModels.Bug;
using Bugget.Entities.DTO.Bug;

namespace Bugget.BO.Services.Bugs
{
    public class BugEventsService(
            IReportPageHubClient reportPageHubClient,
            ParticipantsService participantsService,
            CommentLogsService commentLogsService)
    {
        public async Task HandleCreateBugEventAsync(ReportIdContext reportIdContext, UserIdentity user, BugSummaryDbModel result)
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
}
