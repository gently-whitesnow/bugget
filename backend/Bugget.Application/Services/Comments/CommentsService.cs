using System.Text.Json;
using Bugget.Application.Commands.Comment;
using Bugget.Application.DomainEvents;
using Bugget.Application.Errors;
using Bugget.Application.Ports;
using Bugget.Application.Services.Attachments;
using Bugget.Application.Services.Bugs;
using Bugget.Application.Services.Reports;
using Bugget.Domain;
using Bugget.Domain.Attachments;
using Bugget.Domain.Authentication;
using Bugget.Domain.Comments;
using Bugget.Domain.Common;
using Bugget.Domain.DomainEvents;
using Bugget.Domain.Errors;
using Bugget.Domain.Reports;

namespace Bugget.Application.Services.Comments;

public sealed class CommentsService(
    ICommentsDbClient commentsDbClient,
    CommentEventsService commentEventsService,
    AttachmentBatchWriter attachmentBatchWriter,
    ITaskQueue taskQueue,
    IReportsService reportsService,
    IBugsService bugsService,
    IDomainEventPublisher domainEventPublisher,
    IUnitOfWork unitOfWork) : ICommentsService
{
    public async Task<(CommentSummary? Value, Error? Error)> CreateCommentAsync(UserIdentity user, string aliasId, int bugId, CommentDto commentDto)
    {
        var (resolvedReport, error) = await ResolveBugReportAsync(user, aliasId, bugId);
        if (resolvedReport == null)
        {
            return (null, error);
        }

        var comment = await unitOfWork.ExecuteAsync((scope, ct) =>
            CreateInScopeAsync(scope, user, resolvedReport, bugId, commentDto, ct));

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () => await commentEventsService.HandleCommentCreateEventAsync(reportIdContext, user, comment));

        return (comment, null);
    }

    public async Task<(Comment? Value, Error? Error)> CreateCommentWithAttachmentsAsync(
        UserIdentity user,
        string aliasId,
        int bugId,
        CommentDto commentDto,
        IReadOnlyList<AttachmentUpload> files,
        CancellationToken ct)
    {
        var (resolvedReport, error) = await ResolveBugReportAsync(user, aliasId, bugId);
        error ??= AttachmentBatchWriter.Validate(files);
        if (resolvedReport == null || error != null)
        {
            return (null, error);
        }

        var (summary, attachments) = await attachmentBatchWriter.CreateAsync(
            new AttachmentBatchTarget(user, resolvedReport.Id, AttachType.Comment),
            files,
            async (scope, token) =>
            {
                var created = await CreateInScopeAsync(scope, user, resolvedReport, bugId, commentDto, token);
                return (created, created.Id);
            },
            ct);

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async queueToken =>
        {
            await commentEventsService.HandleCommentCreateEventAsync(reportIdContext, user, summary);
            await attachmentBatchWriter.PublishCreatedAsync(reportIdContext, user, attachments, queueToken);
        });

        return (ToComment(summary, attachments), null);
    }

    private async Task<(ResolvedReportId? Report, Error? Error)> ResolveBugReportAsync(UserIdentity user, string aliasId, int bugId)
    {
        var resolvedReport = await reportsService.ResolveReportByAliasAsync(aliasId, user);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var bug = await bugsService.GetBugAsync(resolvedReport.Id, bugId);
        return bug == null ? (null, BoErrors.BugNotFoundError) : (resolvedReport, null);
    }

    private async Task<CommentSummary> CreateInScopeAsync(
        ITransactionScope scope,
        UserIdentity user,
        ResolvedReportId resolvedReport,
        int bugId,
        CommentDto commentDto,
        CancellationToken ct)
    {
        var audience = (int)(commentDto.Audience.HasValue
            ? (CommentAudience)commentDto.Audience.Value
            : CommentAudience.Internal);

        var summary = await commentsDbClient.CreateCommentAsync(
            scope, user.Id, bugId, commentDto.Text,
            creatorType: (int)user.ActorCreatorType,
            audience: audience);

        var payload = JsonSerializer.Serialize(new
        {
            commentId = summary.Id,
            bugId = summary.BugId,
            text = summary.Text,
            audience = summary.Audience,
            creatorType = summary.CreatorType,
            creatorUserId = summary.CreatorUserId,
            attachments = Array.Empty<object>(),
        });

        await domainEventPublisher.PublishAsync(new DomainEvent
        {
            WorkspaceId = BugsService.ResolveWorkspaceId(resolvedReport.CreatorTeamId, user.OrganizationId),
            AggregateType = BuggetAggregateTypes.Comment,
            AggregateId = summary.Id.ToString(),
            EventType = BuggetEventTypes.CommentCreated,
            Payload = payload,
            ActorUserId = user.Id,
            ActorCreatorType = (short)summary.CreatorType,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
        }, scope, ct);

        return summary;
    }

    private static Comment ToComment(CommentSummary summary, Attachment[] attachments) => new()
    {
        Id = summary.Id,
        BugId = summary.BugId,
        Text = summary.Text,
        CreatorUserId = summary.CreatorUserId,
        CreatorType = summary.CreatorType,
        Audience = summary.Audience,
        CreatedAt = summary.CreatedAt,
        UpdatedAt = summary.UpdatedAt,
        Attachments = attachments,
    };

    public async Task<Error?> DeleteCommentAsync(UserIdentity user, string aliasId, int bugId, int commentId)
    {
        var resolvedReport = await reportsService.ResolveReportByAliasAsync(aliasId, user);
        if (resolvedReport == null)
        {
            return BoErrors.ReportNotFoundError;
        }

        var comment = await commentsDbClient.DeleteCommentInternalAsync(user.Id, resolvedReport.Id, bugId, commentId);
        if (comment == null)
        {
            return null;
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () => await commentEventsService.HandleCommentDeleteEventAsync(reportIdContext, user, bugId, commentId));

        return null;
    }

    public async Task<(CommentSummary? Value, Error? Error)> UpdateCommentAsync(UserIdentity user, string aliasId, int bugId, int commmentId, CommentDto commentDto)
    {
        var resolvedReport = await reportsService.ResolveReportByAliasAsync(aliasId, user);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var comment = await commentsDbClient.UpdateCommentInternalAsync(user.Id, resolvedReport.Id, bugId, commmentId, commentDto.Text);
        if (comment == null)
        {
            return (null, BoErrors.UserCommentNotFound);
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () => await commentEventsService.HandleCommentUpdateEventAsync(reportIdContext, user, comment));

        return (comment, null);
    }
}
