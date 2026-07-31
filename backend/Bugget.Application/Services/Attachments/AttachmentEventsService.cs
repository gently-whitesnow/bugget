using System.Text.Json;
using Bugget.Application.DomainEvents;
using Bugget.Application.Interfaces;
using Bugget.Application.Mappers;
using Bugget.Application.Ports;
using Bugget.Application.Services.Bugs;
using Bugget.Domain;
using Bugget.Domain.Attachments;
using Bugget.Domain.Authentication;
using Bugget.Domain.DomainEvents;
using Bugget.Domain.Reports;
using Microsoft.Extensions.Logging;

namespace Bugget.Application.Services.Attachments;

public class AttachmentEventsService(
    IReportPageHubClient reportPageHubClient,
    IFileStorageClient fileStorageClient,
    IAttachmentKeyGenerator keyGen,
    AttachmentOptimizator attachmentOptimizator,
    ICommentsDbClient commentsDbClient,
    IDomainEventPublisher domainEventPublisher,
    IUnitOfWork unitOfWork,
    ILogger<AttachmentEventsService> logger
        )
{
    public async Task HandleAttachmentCreateEventAsync(ReportIdContext reportIdContext, UserIdentity user, Attachment attachment)
    {
        await Task.WhenAll(
            reportPageHubClient.SendAttachmentCreateAsync(reportIdContext.GroupKey, attachment.ToSocketView(), user.SignalRConnectionId),
            attachmentOptimizator.OptimizeAttachmentAsync(user.OrganizationId, reportIdContext, attachment),
            PublishDomainEventAsync(reportIdContext, user, attachment));
    }

    public async Task HandleAttachmentDeleteEventAsync(ReportIdContext reportIdContext, UserIdentity user, Attachment attachment)
    {
        var tasks = new List<Task>
        {
            reportPageHubClient.SendAttachmentDeleteAsync(reportIdContext.GroupKey, attachment.Id, attachment.EntityId, attachment.AttachType, user.SignalRConnectionId),
        };
        if (attachment.StorageKey is not null)
        {
            tasks.Add(fileStorageClient.DeleteAsync(attachment.StorageKey));
            if (attachment.HasPreview == true)
            {
                tasks.Add(fileStorageClient.DeleteAsync(keyGen.GetPreviewKey(attachment.StorageKey)));
            }
        }
        await Task.WhenAll(tasks);
    }

    public Task HandleAttachmentRenameEventAsync(ReportIdContext reportIdContext, Attachment attachment)
    {
        return reportPageHubClient.SendAttachmentChangedAsync(reportIdContext.GroupKey, attachment.ToSocketView());
    }

    private Task PublishDomainEventAsync(
        ReportIdContext reportIdContext,
        UserIdentity user,
        Attachment attachment)
    {
        if (attachment.AttachType != (int)AttachType.Comment)
        {
            return Task.CompletedTask;
        }

        return PublishCommentAttachmentCreatedAsync(reportIdContext, user, attachment);
    }

    private async Task PublishCommentAttachmentCreatedAsync(
        ReportIdContext reportIdContext,
        UserIdentity user,
        Attachment attachment)
    {
        var comment = await commentsDbClient.GetCommentAsync(attachment.EntityId);
        if (comment is null)
        {
            logger.LogWarning(
                "attachment.create domain event skipped: comment not found comment_id={CommentId} attachment_id={AttachmentId}",
                attachment.EntityId, attachment.Id);
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            attachmentId = attachment.Id,
            commentId = comment.Id,
            bugId = comment.BugId,
            attachType = attachment.AttachType,
            fileName = attachment.FileName,
            contentType = attachment.MimeType,
            creatorType = comment.CreatorType,
            creatorUserId = comment.CreatorUserId,
            audience = comment.Audience,
        });

        await unitOfWork.ExecuteAsync((scope, ct) =>
            domainEventPublisher.PublishAsync(new DomainEvent
            {
                WorkspaceId = BugsService.ResolveWorkspaceId(reportIdContext.TeamId, user.OrganizationId),
                AggregateType = BuggetAggregateTypes.Attachment,
                AggregateId = attachment.Id.ToString(),
                EventType = BuggetEventTypes.AttachmentCreated,
                Payload = payload,
                ActorUserId = comment.CreatorUserId,
                ActorCreatorType = (short)comment.CreatorType,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid(),
            }, scope, ct));
    }
}
