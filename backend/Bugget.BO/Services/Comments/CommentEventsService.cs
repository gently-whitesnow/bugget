using Bugget.BO.Ports;
using Bugget.BO.Services;
using Bugget.BO.Services.Attachments;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.Comments;
using Bugget.Entities.BO.ReportBo;

namespace Bugget.BO.Services.Comments;

public class CommentEventsService(
        IReportPageHubClient reportPageHubClient,
        AttachmentService attachmentService,
        ParticipantsService participantsService
            )
{
    public async Task HandleCommentCreateEventAsync(ReportIdContext reportIdContext, UserIdentity user, CommentSummary commentSummaryDbModel)
    {
        await Task.WhenAll(
            participantsService.AddParticipantIfNotExistAsync(reportIdContext, user.Id),
            reportPageHubClient.SendCommentCreateAsync(reportIdContext.GroupKey, commentSummaryDbModel, user.SignalRConnectionId)
    );
    }

    public async Task HandleCommentDeleteEventAsync(ReportIdContext reportIdContext, UserIdentity user, int bugId, int commentId)
    {
        await Task.WhenAll(
            reportPageHubClient.SendCommentDeleteAsync(reportIdContext.GroupKey, bugId, commentId, user.SignalRConnectionId),
            attachmentService.DeleteCommentAttachmentsInternalAsync(commentId)
        );
    }

    public async Task HandleCommentUpdateEventAsync(ReportIdContext reportIdContext, UserIdentity user, CommentSummary commentSummaryDbModel)
    {
        await Task.WhenAll(
            reportPageHubClient.SendCommentUpdateAsync(reportIdContext.GroupKey, commentSummaryDbModel, user.SignalRConnectionId)
        );
    }
}

