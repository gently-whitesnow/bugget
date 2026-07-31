using System.Text.Json;
using Bugget.BO.DomainEvents;
using Bugget.BO.Errors;
using Bugget.BO.Ports;
using Bugget.BO.Services.Bugs;
using Bugget.BO.Services.Reports;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.Comments;
using Bugget.Entities.BO.Common;
using Bugget.Entities.BO.DomainEvents;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DTO.Comment;
using Bugget.Entities.Errors;
using Bugget.Entities.Options;
using Microsoft.Extensions.Options;
using TaskQueue;

namespace Bugget.BO.Services.Comments;

public sealed class CommentsService(
    ICommentsDbClient commentsDbClient,
    CommentEventsService commentEventsService,
    ITaskQueue taskQueue,
    ReportsService reportsService,
    IOptions<ReportAliasOptions> aliasOptions,
    BugsService bugsService,
    IDomainEventPublisher domainEventPublisher,
    IUnitOfWork unitOfWork)
{
    public async Task<(CommentSummary? Value, Error? Error)> CreateCommentAsync(UserIdentity user, string aliasId, int bugId, CommentDto commentDto)
    {
        var (reportId, publicId, teamReportId) = ReportIdResolveHelper.ResolveReportId(aliasId, aliasOptions.Value);
        var resolvedReport = await reportsService.ResolveReportIdAsync(
            user.OrganizationId,
            user.TeamId,
            reportId,
            publicId,
            teamReportId
        );
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var bugDbModel = await bugsService.GetBugAsync(resolvedReport.Id, bugId);
        if (bugDbModel == null)
        {
            return (null, BoErrors.BugNotFoundError);
        }

        var commentDbModel = await unitOfWork.ExecuteAsync(async (scope, ct) =>
        {
            var audience = (int)(commentDto.Audience.HasValue
                ? (CommentAudience)commentDto.Audience.Value
                : CommentAudience.Internal);

            var summary = await commentsDbClient.CreateCommentAsync(
                scope, user.Id, bugId, commentDto.Text,
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
        });

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () => await commentEventsService.HandleCommentCreateEventAsync(reportIdContext, user, commentDbModel));

        return (commentDbModel, null);
    }

    public async Task<Error?> DeleteCommentAsync(UserIdentity user, string aliasId, int bugId, int commentId)
    {
        var (reportId, publicId, teamReportId) = ReportIdResolveHelper.ResolveReportId(aliasId, aliasOptions.Value);
        var resolvedReport = await reportsService.ResolveReportIdAsync(
            user.OrganizationId,
            user.TeamId,
            reportId,
            publicId,
            teamReportId
        );
        if (resolvedReport == null)
        {
            return BoErrors.ReportNotFoundError;
        }

        var commentDbModel = await commentsDbClient.DeleteCommentInternalAsync(user.Id, resolvedReport.Id, bugId, commentId);
        if (commentDbModel == null)
        {
            return null;
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () => await commentEventsService.HandleCommentDeleteEventAsync(reportIdContext, user, bugId, commentId));

        return null;
    }

    public async Task<(CommentSummary? Value, Error? Error)> UpdateCommentAsync(UserIdentity user, string aliasId, int bugId, int commmentId, CommentDto commentDto)
    {
        var (reportId, publicId, teamReportId) = ReportIdResolveHelper.ResolveReportId(aliasId, aliasOptions.Value);
        var resolvedReport = await reportsService.ResolveReportIdAsync(
            user.OrganizationId,
            user.TeamId,
            reportId,
            publicId,
            teamReportId
        );
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var commentDbModel = await commentsDbClient.UpdateCommentInternalAsync(user.Id, resolvedReport.Id, bugId, commmentId, commentDto.Text);
        if (commentDbModel == null)
        {
            return (null, BoErrors.UserCommentNotFound);
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(async () => await commentEventsService.HandleCommentUpdateEventAsync(reportIdContext, user, commentDbModel));

        return (commentDbModel, null);
    }
}
