using System.Text.Json;
using Bugget.Application.Commands.Bug;
using Bugget.Application.DomainEvents;
using Bugget.Application.Errors;
using Bugget.Application.Ports;
using Bugget.Application.Services.Reports;
using Bugget.Domain.Authentication;
using Bugget.Domain.Bugs;
using Bugget.Domain.DomainEvents;
using Bugget.Domain.Errors;
using Bugget.Domain.Reports;

namespace Bugget.Application.Services.Bugs;

public sealed class BugsService(
    IBugsDbClient bugsDbClient,
    BugEventsService bugsEventsService,
    ITaskQueue taskQueue,
    IReportsService reportsService,
    IDomainEventPublisher domainEventPublisher,
    IUnitOfWork unitOfWork) : IBugsService
{
    public async Task<(BugSummary? Value, Error? Error)> CreateBugAsync(UserIdentity user, string aliasId, BugDto bug)
    {
        if (string.IsNullOrEmpty(bug.Expect) && string.IsNullOrEmpty(bug.Receive))
        {
            return (null, BoErrors.BugMustHaveOneField);
        }

        var resolvedReport = await reportsService.ResolveReportByAliasAsync(aliasId, user);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var creatorType = (int)user.ActorCreatorType;

        var bugSummary = await unitOfWork.ExecuteAsync(async (scope, ct) =>
        {
            var summary = await bugsDbClient.CreateBugAsync(
                scope, user.Id, resolvedReport.Id, bug, creatorType);

            var payload = JsonSerializer.Serialize(new
            {
                bugId = summary.Id,
                reportId = resolvedReport.Id,
                title = summary.Title,
                creatorType = summary.CreatorType,
                creatorUserId = summary.CreatorUserId,
            });

            await domainEventPublisher.PublishAsync(new DomainEvent
            {
                WorkspaceId = ResolveWorkspaceId(resolvedReport.CreatorTeamId, user.OrganizationId),
                AggregateType = BuggetAggregateTypes.Bug,
                AggregateId = summary.Id.ToString(),
                EventType = BuggetEventTypes.BugCreated,
                Payload = payload,
                ActorUserId = user.Id,
                ActorCreatorType = (short)summary.CreatorType,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid(),
            }, scope, ct);

            return summary;
        });

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(() => bugsEventsService.HandleCreateBugEventAsync(reportIdContext, user, bugSummary));
        return (bugSummary, null);
    }

    public async Task<(BugPatchResult? Value, Error? Error)> PatchBugAsync(UserIdentity user, string aliasId, int bugId, BugPatchDto patchDto)
    {
        var resolvedReport = await reportsService.ResolveReportByAliasAsync(aliasId, user);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var actorCreatorType = (short)user.ActorCreatorType;

        var bugPatchResult = await unitOfWork.ExecuteAsync(async (scope, ct) =>
        {
            int? oldStatus = null;
            if (patchDto.Status.HasValue)
            {
                var existing = await bugsDbClient.GetBugAsync(scope, resolvedReport.Id, bugId);
                oldStatus = existing?.Status;
            }

            var patchResult = await bugsDbClient.PatchBugAsync(scope, resolvedReport.Id, bugId, patchDto);

            if (oldStatus.HasValue && oldStatus.Value != patchResult.Status)
            {
                var payload = JsonSerializer.Serialize(new
                {
                    bugId,
                    reportId = resolvedReport.Id,
                    oldStatus = oldStatus.Value,
                    newStatus = patchResult.Status,
                    actorUserId = user.Id,
                });

                await domainEventPublisher.PublishAsync(new DomainEvent
                {
                    WorkspaceId = ResolveWorkspaceId(resolvedReport.CreatorTeamId, user.OrganizationId),
                    AggregateType = BuggetAggregateTypes.Bug,
                    AggregateId = bugId.ToString(),
                    EventType = BuggetEventTypes.BugStatusChanged,
                    Payload = payload,
                    ActorUserId = user.Id,
                    ActorCreatorType = actorCreatorType,
                    OccurredAt = DateTimeOffset.UtcNow,
                    CorrelationId = Guid.NewGuid(),
                }, scope, ct);
            }

            return patchResult;
        });

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(() => bugsEventsService.HandlePatchBugEventAsync(reportIdContext, bugId, user, patchDto));
        return (bugPatchResult, null);
    }

    public async Task<BugSummary?> GetBugAsync(int reportId, int bugId)
    {
        return await bugsDbClient.GetBugAsync(reportId, bugId);
    }

    internal static string ResolveWorkspaceId(string? teamId, string? organizationId)
        => teamId ?? organizationId ?? "unknown";
}
