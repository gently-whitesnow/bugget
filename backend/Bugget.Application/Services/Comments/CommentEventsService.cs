using Bugget.Application.Ports;
using Bugget.Application.Services;
using Bugget.Application.Services.Attachments;
using Bugget.Domain.Authentication;
using Bugget.Domain.Comments;
using Bugget.Domain.Reports;

namespace Bugget.Application.Services.Comments;

public class CommentEventsService(
        IReportPageHubClient reportPageHubClient,
        AttachmentService attachmentService,
        ParticipantsService participantsService
            )
{
    public async Task HandleCommentCreateEventAsync(ReportIdContext reportIdContext, UserIdentity user, CommentSummary commentSummary)
    {
        await Task.WhenAll(
            participantsService.AddParticipantIfNotExistAsync(reportIdContext, user.Id),
            reportPageHubClient.SendCommentCreateAsync(reportIdContext.GroupKey, commentSummary, user.SignalRConnectionId)
    );
    }

    public async Task HandleCommentDeleteEventAsync(ReportIdContext reportIdContext, UserIdentity user, int bugId, int commentId)
    {
        await Task.WhenAll(
            reportPageHubClient.SendCommentDeleteAsync(reportIdContext.GroupKey, bugId, commentId, user.SignalRConnectionId),
            attachmentService.DeleteCommentAttachmentsInternalAsync(commentId)
        );
    }

    public async Task HandleCommentUpdateEventAsync(ReportIdContext reportIdContext, UserIdentity user, CommentSummary commentSummary)
    {
        await Task.WhenAll(
            reportPageHubClient.SendCommentUpdateAsync(reportIdContext.GroupKey, commentSummary, user.SignalRConnectionId)
        );
    }
}

