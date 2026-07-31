using Bugget.Entities.BO.Bugs;
using Bugget.Entities.BO.Comments;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DTO.Bug;
using Bugget.Entities.SocketViews;

namespace Bugget.BO.Ports;

public interface IReportPageHubClient
{
    Task SendReportPatchAsync(string groupKey, PatchReportSocketView view, string? signalRConnectionId);
    Task SendNewReportParticipantAsync(string groupKey, string newParticipant);
    Task SendBugCreateAsync(string groupKey, BugSummary summary, string? signalRConnectionId);
    Task SendBugPatchAsync(string groupKey, int bugId, BugPatchDto patchDto, string? signalRConnectionId);
    Task SendAttachmentCreateAsync(string groupKey, AttachmentSocketView attachmentSocketView, string? signalRConnectionId);
    Task SendAttachmentDeleteAsync(string groupKey, int id, int entityId, int attachType, string? signalRConnectionId);
    Task SendAttachmentChangedAsync(string groupKey, AttachmentSocketView attachmentSocketView);
    Task SendCommentCreateAsync(string groupKey, CommentSummary comment, string? signalRConnectionId);
    Task SendCommentDeleteAsync(string groupKey, int bugId, int commentId, string? signalRConnectionId);
    Task SendCommentUpdateAsync(string groupKey, CommentSummary comment, string? signalRConnectionId);
    Task SendReportLinkCreateAsync(string groupKey, ReportLink link, string? signalRConnectionId);
    Task SendReportLinkUpdateAsync(string groupKey, ReportLink link, string? signalRConnectionId);
    Task SendReportLinkDeleteAsync(string groupKey, int linkId, string? signalRConnectionId);
    Task SendBugStepCreateAsync(string groupKey, BugStepSummary step, string? signalRConnectionId);
    Task SendBugStepPatchAsync(string groupKey, int bugId, BugStepSummary step, string? signalRConnectionId);
    Task SendBugStepsOrderUpdateAsync(string groupKey, int bugId, BugStepSummary[] steps, string? signalRConnectionId);
    Task SendBugStepDeleteAsync(string groupKey, int bugId, int stepId, string? signalRConnectionId);
}
