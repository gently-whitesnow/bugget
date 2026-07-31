using System.Text.Json;
using Bugget.BO.DomainEvents;
using Bugget.BO.Interfaces;
using Bugget.BO.Mappers;
using Bugget.BO.Ports;
using Bugget.BO.Services.Bugs;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO;
using Bugget.Entities.BO.AttachmentBo;
using Bugget.Entities.BO.DomainEvents;
using Bugget.Entities.BO.ReportBo;
using Microsoft.Extensions.Logging;

namespace Bugget.BO.Services.Attachments;

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
    public async Task HandleAttachmentCreateEventAsync(ReportIdContext reportIdContext, UserIdentity user, Attachment attachmentDbModel)
    {
        await Task.WhenAll(
            reportPageHubClient.SendAttachmentCreateAsync(reportIdContext.GroupKey, attachmentDbModel.ToSocketView(), user.SignalRConnectionId),
            attachmentOptimizator.OptimizeAttachmentAsync(user.OrganizationId, reportIdContext, attachmentDbModel),
            PublishDomainEventAsync(reportIdContext, user, attachmentDbModel));
    }

    public async Task HandleAttachmentDeleteEventAsync(ReportIdContext reportIdContext, UserIdentity user, Attachment attachmentDbModel)
    {
        var tasks = new List<Task>
        {
            reportPageHubClient.SendAttachmentDeleteAsync(reportIdContext.GroupKey, attachmentDbModel.Id, attachmentDbModel.EntityId, attachmentDbModel.AttachType, user.SignalRConnectionId),
        };
        if (attachmentDbModel.StorageKey is not null)
        {
            tasks.Add(fileStorageClient.DeleteAsync(attachmentDbModel.StorageKey));
            if (attachmentDbModel.HasPreview == true)
            {
                tasks.Add(fileStorageClient.DeleteAsync(keyGen.GetPreviewKey(attachmentDbModel.StorageKey)));
            }
        }
        await Task.WhenAll(tasks);
    }

    public Task HandleAttachmentRenameEventAsync(ReportIdContext reportIdContext, Attachment attachmentDbModel)
    {
        return reportPageHubClient.SendAttachmentChangedAsync(reportIdContext.GroupKey, attachmentDbModel.ToSocketView());
    }

    private Task PublishDomainEventAsync(
        ReportIdContext reportIdContext,
        UserIdentity user,
        Attachment attachmentDbModel)
    {
        if (attachmentDbModel.AttachType != (int)AttachType.Comment)
        {
            return Task.CompletedTask;
        }

        return PublishCommentAttachmentCreatedAsync(reportIdContext, user, attachmentDbModel);
    }

    private async Task PublishCommentAttachmentCreatedAsync(
        ReportIdContext reportIdContext,
        UserIdentity user,
        Attachment attachmentDbModel)
    {
        var comment = await commentsDbClient.GetCommentAsync(attachmentDbModel.EntityId);
        if (comment is null)
        {
            logger.LogWarning(
                "attachment.create domain event skipped: comment not found comment_id={CommentId} attachment_id={AttachmentId}",
                attachmentDbModel.EntityId, attachmentDbModel.Id);
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            attachmentId = attachmentDbModel.Id,
            commentId = comment.Id,
            bugId = comment.BugId,
            attachType = attachmentDbModel.AttachType,
            fileName = attachmentDbModel.FileName,
            contentType = attachmentDbModel.MimeType,
            creatorType = comment.CreatorType,
            creatorUserId = comment.CreatorUserId,
            audience = comment.Audience,
        });

        await unitOfWork.ExecuteAsync((scope, ct) =>
            domainEventPublisher.PublishAsync(new DomainEvent
            {
                WorkspaceId = BugsService.ResolveWorkspaceId(reportIdContext.TeamId, user.OrganizationId),
                AggregateType = BuggetAggregateTypes.Attachment,
                AggregateId = attachmentDbModel.Id.ToString(),
                EventType = BuggetEventTypes.AttachmentCreated,
                Payload = payload,
                ActorUserId = comment.CreatorUserId,
                ActorCreatorType = (short)comment.CreatorType,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid(),
            }, scope, ct));
    }
}
