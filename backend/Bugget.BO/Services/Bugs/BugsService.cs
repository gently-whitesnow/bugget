using System.Text.Json;
using Bugget.BO.DomainEvents;
using Bugget.BO.Errors;
using Bugget.BO.Ports;
using Bugget.BO.Services.Reports;
using Bugget.Entities.Authentication;
using Bugget.Entities.BO.Bugs;
using Bugget.Entities.BO.DomainEvents;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DTO.Bug;
using Bugget.Entities.Errors;
using Bugget.Entities.Options;
using Microsoft.Extensions.Options;
using TaskQueue;

namespace Bugget.BO.Services.Bugs;

public sealed class BugsService(
    IBugsDbClient bugsDbClient,
    BugEventsService bugsEventsService,
    ITaskQueue taskQueue,
    ReportsService reportsService,
    IOptions<ReportAliasOptions> aliasOptions,
    IDomainEventPublisher domainEventPublisher,
    IUnitOfWork unitOfWork)
{
    public async Task<(BugSummary? Value, Error? Error)> CreateBugAsync(UserIdentity user, string aliasId, BugDto bug)
    {
        if (string.IsNullOrEmpty(bug.Expect) && string.IsNullOrEmpty(bug.Receive))
        {
            return (null, BoErrors.BugMustHaveOneField);
        }
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

        var bugSummary = await unitOfWork.ExecuteAsync(async (scope, ct) =>
        {
            var summary = await bugsDbClient.CreateBugAsync(scope, user.Id, resolvedReport.Id, bug);

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
