using System.Text.Json;
using Bugget.BO.DomainEvents;
using Bugget.BO.Errors;
using Bugget.BO.Services.Bugs;
using Bugget.BO.Services.Reports;
using Bugget.DA.Interfaces;
using Bugget.DA.Transactions;
using Bugget.DA.WebSockets;
using Bugget.Entities.BO.Common;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DbModels.Comment;
using Bugget.Entities.DbModels.DomainEvents;
using Bugget.Entities.DTO.Internal;
using Bugget.Entities.Options;
using Microsoft.Extensions.Options;
using Monade;

namespace Bugget.BO.Services.Internal;

/// <summary>
/// _internal write/read пара для диалога с тестером (TECHSPEC §4.3.3/§4.3.4):
/// <list type="bullet">
///   <item>POST <c>/v2/_internal/bugs/{bugId}/comments</c> — reply-mode ответ тестера;
///     сервер silently форсит <c>audience = External</c> (caller поле не шлёт);</item>
///   <item>GET <c>/v2/_internal/bugs/{bugId}/external-comments</c> — история
///     внешних комментариев; DTO не проектирует <c>audience</c>, SQL жёстко
///     фильтрует External — I-1 инвариант.</item>
/// </list>
/// </summary>
public sealed class InternalCommentsService(
    IBugsDbClient bugsDbClient,
    ICommentsDbClient commentsDbClient,
    IReportsDbClient reportsDbClient,
    IDomainEventPublisher domainEventPublisher,
    IReportPageHubClient reportPageHubClient,
    IOptions<ReportAliasOptions> aliasOptions,
    IUnitOfWork unitOfWork)
{
    private const int DefaultListLimit = 50;
    private const int MaxListLimit = 200;

    public async Task<MonadeStruct<InternalCreateCommentResponseDto>> CreateAsync(
        int bugId,
        InternalCreateCommentRequestDto request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CreatorUserId))
        {
            return BoErrors.CreatorUserIdRequired;
        }

        var locator = await bugsDbClient.GetBugLocatorAsync(bugId);
        if (locator is null)
        {
            return BoErrors.BugNotFoundError;
        }

        var result = await unitOfWork.ExecuteAsync<MonadeStruct<CommentSummaryDbModel>>(async (scope, c) =>
        {
            var reportStatus = await reportsDbClient.GetStatusInternalAsync(scope, locator.ReportId, c);
            if (reportStatus is null)
            {
                return BoErrors.ReportNotFoundError;
            }

            if (IsClosedStatus(reportStatus.Value))
            {
                return BoErrors.ReportClosedError;
            }

            var comment = await commentsDbClient.CreateCommentAsync(
                scope,
                userId: request.CreatorUserId,
                bugId: bugId,
                text: request.Text,
                creatorType: request.CreatorType,
                audience: (int)CommentAudience.External);

            var payload = JsonSerializer.Serialize(new
            {
                commentId = comment.Id,
                bugId = comment.BugId,
                text = comment.Text,
                audience = comment.Audience,
                creatorType = comment.CreatorType,
                creatorUserId = comment.CreatorUserId,
                attachments = Array.Empty<object>(),
            });

            await domainEventPublisher.PublishAsync(new DomainEventDbModel
            {
                WorkspaceId = BugsService.ResolveWorkspaceId(locator.CreatorTeamId, locator.CreatorOrganizationId),
                AggregateType = BuggetAggregateTypes.Comment,
                AggregateId = comment.Id.ToString(),
                EventType = BuggetEventTypes.CommentCreated,
                Payload = payload,
                ActorUserId = comment.CreatorUserId,
                ActorCreatorType = (short)comment.CreatorType,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid(),
            }, scope, c);

            return comment;
        }, ct);

        if (result.HasError)
        {
            return result.Error!;
        }

        var createdComment = result.Value!;

        // Realtime push в «Диалог с тестером» открытых веб-клиентов: бот не SignalR-клиент,
        // sender-suppression не нужен → connectionId = null. AliasId считаем через серверный
        // ReportAliasMode (Default/Guid/Team), чтобы группа совпала с той, в которую
        // join'ятся реальные клиенты — иначе SaaS (Guid) и Team-режим не получают push.
        var aliasId = ReportIdResolveHelper.ToAliasId(
            locator.ReportId,
            locator.PublicId,
            locator.TeamReportId,
            aliasOptions.Value);
        var groupKey = new ReportIdContext(locator.ReportId, aliasId, locator.CreatorTeamId).GroupKey;
        await reportPageHubClient.SendCommentCreateAsync(groupKey, createdComment, null);

        return new InternalCreateCommentResponseDto { CommentId = createdComment.Id };
    }

    public async Task<MonadeStruct<InternalExternalCommentsResponseDto>> ListExternalAsync(
        int bugId,
        int sinceId,
        int? limit,
        CancellationToken ct = default)
    {
        var locator = await bugsDbClient.GetBugLocatorAsync(bugId);
        if (locator is null)
        {
            return BoErrors.BugNotFoundError;
        }

        var resolvedLimit = limit is null
            ? DefaultListLimit
            : Math.Clamp(limit.Value, 1, MaxListLimit);

        var rows = await commentsDbClient.ListExternalCommentsByBugAsync(bugId, sinceId, resolvedLimit);
        var items = rows.Select(r => new InternalExternalCommentItemDto
        {
            Id = r.Id,
            BugId = r.BugId,
            Text = r.Text,
            CreatorType = r.CreatorType,
            CreatorUserId = r.CreatorUserId,
            CreatedAt = r.CreatedAt,
        }).ToArray();

        int? nextSinceId = items.Length == resolvedLimit ? items[^1].Id : null;

        return new InternalExternalCommentsResponseDto
        {
            Items = items,
            NextSinceId = nextSinceId,
        };
    }

    private static bool IsClosedStatus(int status)
        => status == (int)ReportStatus.Resolved || status == (int)ReportStatus.Rejected;
}
