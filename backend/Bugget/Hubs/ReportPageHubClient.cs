using Bugget.BO.Ports;
using Bugget.Entities.BO;
using Bugget.Entities.BO.Bugs;
using Bugget.Entities.BO.Comments;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DTO.Bug;
using Bugget.Entities.SocketViews;
using Microsoft.AspNetCore.SignalR;

namespace Bugget.Hubs;

public class ReportPageHubClient(IHubContext<ReportPageHub> hubContext) : IReportPageHubClient
{
    public Task SendReportPatchAsync(string groupKey, PatchReportSocketView view, string? signalRConnectionId)
    {
        if (signalRConnectionId == null)
        {
            return hubContext.Clients.Group($"{groupKey}")
                .SendAsync("ReceiveReportPatch", view);
        }

        return hubContext.Clients.GroupExcept($"{groupKey}", signalRConnectionId)
            .SendAsync("ReceiveReportPatch", view);
    }

    public Task SendNewReportParticipantAsync(string groupKey, string newParticipant)
    {
        return hubContext.Clients.Group($"{groupKey}")
            .SendAsync("ReceiveReportParticipant", newParticipant);
    }

    public Task SendBugPatchAsync(string groupKey, int bugId, BugPatchDto patchDto, string? signalRConnectionId)
    {
        if (signalRConnectionId == null)
        {
            return hubContext.Clients.Group($"{groupKey}")
                .SendAsync("ReceiveBugPatch", bugId, patchDto);
        }

        return hubContext.Clients.GroupExcept($"{groupKey}", signalRConnectionId)
            .SendAsync("ReceiveBugPatch", bugId, patchDto);
    }

    public Task SendBugCreateAsync(string groupKey, BugSummary summary, string? signalRConnectionId)
    {
        if (signalRConnectionId == null)
        {
            return hubContext.Clients.Group($"{groupKey}")
                .SendAsync("ReceiveBugCreate", summary);
        }

        return hubContext.Clients.GroupExcept($"{groupKey}", signalRConnectionId)
            .SendAsync("ReceiveBugCreate", summary);
    }

    public Task SendAttachmentCreateAsync(string groupKey, AttachmentSocketView attachmentSocketView, string? signalRConnectionId)
    {
        string eventName = attachmentSocketView.AttachType switch
        {
            (int)AttachType.Comment => "ReceiveCommentAttachmentCreate",
            (int)AttachType.BugStep => "ReceiveBugStepAttachmentCreate",
            _ => "ReceiveBugAttachmentCreate"
        };

        if (signalRConnectionId == null)
        {
            return hubContext.Clients.Group($"{groupKey}")
                .SendAsync(eventName, attachmentSocketView);
        }

        return hubContext.Clients.GroupExcept($"{groupKey}", signalRConnectionId)
            .SendAsync(eventName, attachmentSocketView);
    }

    public Task SendAttachmentChangedAsync(string groupKey, AttachmentSocketView attachmentSocketView)
    {
        string eventName = attachmentSocketView.AttachType switch
        {
            (int)AttachType.Comment => "ReceiveCommentAttachmentChanged",
            (int)AttachType.BugStep => "ReceiveBugStepAttachmentChanged",
            _ => "ReceiveBugAttachmentChanged"
        };

        return hubContext.Clients.Group($"{groupKey}")
            .SendAsync(eventName, attachmentSocketView);
    }

    public Task SendAttachmentDeleteAsync(string groupKey, int id, int entityId, int attachType, string? signalRConnectionId)
    {
        string eventName = attachType switch
        {
            (int)AttachType.Comment => "ReceiveCommentAttachmentDelete",
            (int)AttachType.BugStep => "ReceiveBugStepAttachmentDelete",
            _ => "ReceiveBugAttachmentDelete"
        };

        if (signalRConnectionId == null)
        {
            return hubContext.Clients.Group($"{groupKey}")
                .SendAsync(eventName, id, entityId, attachType);
        }

        return hubContext.Clients.GroupExcept($"{groupKey}", signalRConnectionId)
            .SendAsync(eventName, id, entityId, attachType);
    }

    public Task SendCommentCreateAsync(string groupKey, CommentSummary comment, string? signalRConnectionId)
    {
        if (signalRConnectionId == null)
        {
            return hubContext.Clients.Group($"{groupKey}")
                .SendAsync("ReceiveCommentCreate", comment);
        }

        return hubContext.Clients.GroupExcept($"{groupKey}", signalRConnectionId)
            .SendAsync("ReceiveCommentCreate", comment);
    }

    public Task SendCommentDeleteAsync(string groupKey, int bugId, int commentId, string? signalRConnectionId)
    {
        if (signalRConnectionId == null)
        {
            return hubContext.Clients.Group($"{groupKey}")
                .SendAsync("ReceiveCommentDelete", bugId, commentId);
        }

        return hubContext.Clients.GroupExcept($"{groupKey}", signalRConnectionId)
            .SendAsync("ReceiveCommentDelete", bugId, commentId);
    }

    public Task SendCommentUpdateAsync(string groupKey, CommentSummary comment, string? signalRConnectionId)
    {
        if (signalRConnectionId == null)
        {
            return hubContext.Clients.Group($"{groupKey}")
                .SendAsync("ReceiveCommentUpdate", comment);
        }

        return hubContext.Clients.GroupExcept($"{groupKey}", signalRConnectionId)
            .SendAsync("ReceiveCommentUpdate", comment);
    }

    public Task SendReportLinkCreateAsync(string groupKey, ReportLink reportLinkDbModel, string? signalRConnectionId)
    {
        if (signalRConnectionId == null)
        {
            return hubContext.Clients.Group($"{groupKey}")
                .SendAsync("ReceiveReportLinkCreate", reportLinkDbModel);
        }

        return hubContext.Clients.GroupExcept($"{groupKey}", signalRConnectionId)
            .SendAsync("ReceiveReportLinkCreate", reportLinkDbModel);
    }

    public Task SendReportLinkUpdateAsync(string groupKey, ReportLink reportLinkDbModel, string? signalRConnectionId)
    {
        if (signalRConnectionId == null)
        {
            return hubContext.Clients.Group($"{groupKey}")
                .SendAsync("ReceiveReportLinkUpdate", reportLinkDbModel);
        }

        return hubContext.Clients.GroupExcept($"{groupKey}", signalRConnectionId)
            .SendAsync("ReceiveReportLinkUpdate", reportLinkDbModel);
    }

    public Task SendReportLinkDeleteAsync(string groupKey, int linkId, string? signalRConnectionId)
    {
        if (signalRConnectionId == null)
        {
            return hubContext.Clients.Group($"{groupKey}")
                .SendAsync("ReceiveReportLinkDelete", linkId);
        }

        return hubContext.Clients.GroupExcept($"{groupKey}", signalRConnectionId)
            .SendAsync("ReceiveReportLinkDelete", linkId);
    }

    public Task SendBugStepCreateAsync(string groupKey, BugStepSummary step, string? signalRConnectionId)
    {
        if (signalRConnectionId == null)
        {
            return hubContext.Clients.Group($"{groupKey}")
                .SendAsync("ReceiveBugStepCreate", step);
        }

        return hubContext.Clients.GroupExcept($"{groupKey}", signalRConnectionId)
            .SendAsync("ReceiveBugStepCreate", step);
    }

    public Task SendBugStepPatchAsync(string groupKey, int bugId, BugStepSummary step, string? signalRConnectionId)
    {
        if (signalRConnectionId == null)
        {
            return hubContext.Clients.Group($"{groupKey}")
                .SendAsync("ReceiveBugStepPatch", bugId, step);
        }

        return hubContext.Clients.GroupExcept($"{groupKey}", signalRConnectionId)
            .SendAsync("ReceiveBugStepPatch", bugId, step);
    }

    public Task SendBugStepsOrderUpdateAsync(string groupKey, int bugId, BugStepSummary[] steps, string? signalRConnectionId)
    {
        if (signalRConnectionId == null)
        {
            return hubContext.Clients.Group($"{groupKey}")
                .SendAsync("ReceiveBugStepsOrderUpdate", bugId, steps);
        }

        return hubContext.Clients.GroupExcept($"{groupKey}", signalRConnectionId)
            .SendAsync("ReceiveBugStepsOrderUpdate", bugId, steps);
    }

    public Task SendBugStepDeleteAsync(string groupKey, int bugId, int stepId, string? signalRConnectionId)
    {
        if (signalRConnectionId == null)
        {
            return hubContext.Clients.Group($"{groupKey}")
                .SendAsync("ReceiveBugStepDelete", bugId, stepId);
        }

        return hubContext.Clients.GroupExcept($"{groupKey}", signalRConnectionId)
            .SendAsync("ReceiveBugStepDelete", bugId, stepId);
    }
}
