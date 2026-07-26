using Bugget.Entities.DbModels.Bug;
using Bugget.Entities.DbModels.BugSteps;
using Bugget.Entities.DbModels.Comment;
using Bugget.Entities.DbModels.ReportLink;
using Bugget.Entities.DTO.Bug;
using Bugget.Entities.SocketViews;

namespace Bugget.DA.WebSockets;

public interface IReportPageHubClient
{
    Task SendReportPatchAsync(string groupKey, PatchReportSocketView view, string? signalRConnectionId);
    Task SendNewReportParticipantAsync(string groupKey, string newParticipant);
    Task SendBugCreateAsync(string groupKey, BugSummaryDbModel summaryDbModel, string? signalRConnectionId);
    Task SendBugPatchAsync(string groupKey, int bugId, BugPatchDto patchDto, string? signalRConnectionId);
    Task SendAttachmentCreateAsync(string groupKey, AttachmentSocketView attachmentSocketView, string? signalRConnectionId);
    Task SendAttachmentDeleteAsync(string groupKey, int id, int entityId, int attachType, string? signalRConnectionId);
    Task SendAttachmentChangedAsync(string groupKey, AttachmentSocketView attachmentSocketView);
    Task SendCommentCreateAsync(string groupKey, CommentSummaryDbModel commentSummaryDbModel, string? signalRConnectionId);
    Task SendCommentDeleteAsync(string groupKey, int bugId, int commentId, string? signalRConnectionId);
    Task SendCommentUpdateAsync(string groupKey, CommentSummaryDbModel commentSummaryDbModel, string? signalRConnectionId);
    Task SendReportLinkCreateAsync(string groupKey, ReportLinkDbModel linkDbModel, string? signalRConnectionId);
    Task SendReportLinkUpdateAsync(string groupKey, ReportLinkDbModel linkDbModel, string? signalRConnectionId);
    Task SendReportLinkDeleteAsync(string groupKey, int linkId, string? signalRConnectionId);
    Task SendBugStepCreateAsync(string groupKey, BugStepSummaryDbModel bugStepSummaryDbModel, string? signalRConnectionId);
    Task SendBugStepPatchAsync(string groupKey, int bugId, BugStepSummaryDbModel bugStepSummaryDbModel, string? signalRConnectionId);
    Task SendBugStepsOrderUpdateAsync(string groupKey, int bugId, BugStepSummaryDbModel[] bugStepSummaryDbModels, string? signalRConnectionId);
    Task SendBugStepDeleteAsync(string groupKey, int bugId, int stepId, string? signalRConnectionId);
}
