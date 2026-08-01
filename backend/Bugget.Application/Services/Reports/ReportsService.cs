using Bugget.Application.Commands.Report;
using Bugget.Application.DomainEvents;
using Bugget.Application.Errors;
using Bugget.Application.Options;
using Bugget.Application.Ports;
using Bugget.Application.Services.Bugs;
using Bugget.Domain.Authentication;
using Bugget.Domain.Bugs;
using Bugget.Domain.Errors;
using Bugget.Domain.Reports;
using Bugget.Domain.Search;
using Microsoft.Extensions.Options;

namespace Bugget.Application.Services.Reports;

public sealed class ReportsService(
    IReportsDbClient reportsDbClient,
    ITaskQueue taskQueue,
    ReportEventsService reportEventsService,
    IDomainEventPublisher domainEventPublisher,
    IUnitOfWork unitOfWork,
    IOptions<ReportAliasOptions> aliasOptions)
{

    public Task<ReportSummary> CreateReportAsync(string userId, string? teamId, string? organizationId, ReportCreateDto createDto)
    {
        return reportsDbClient.CreateReportAsync(userId, teamId, organizationId, createDto);
    }

    public async Task<(ReportPatchResult? Value, Error? Error)> PatchReportAsync(string aliasId, UserIdentity user, ReportPatchDto patchDto)
    {
        var resolvedReport = await ResolveReportAsync(aliasId, user);
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var workspaceId = BugsService.ResolveWorkspaceId(resolvedReport.CreatorTeamId, user.OrganizationId);

        var (patchResult, effectivePatch) = await unitOfWork.ExecuteAsync(
            (scope, ct) => ApplyStatusPatchInTxAsync(scope, ct, resolvedReport.Id, workspaceId, user, patchDto));

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await taskQueue.EnqueueAsync(() => reportEventsService.HandlePatchReportEventAsync(reportIdContext, user, effectivePatch, patchResult));

        return (patchResult, null);
    }

    private Task<ResolvedReportId?> ResolveReportAsync(string aliasId, UserIdentity user)
    {
        var (reportId, publicId, teamReportId) = ReportIdResolveHelper.ResolveReportId(aliasId, aliasOptions.Value);
        return reportsDbClient.ResolveReportIdAsync(
            user.OrganizationId,
            user.TeamId,
            reportId,
            publicId,
            teamReportId);
    }

    private async Task<(ReportPatchResult PatchResult, ReportPatchDto EffectivePatch)> ApplyStatusPatchInTxAsync(
        ITransactionScope scope,
        CancellationToken ct,
        int reportId,
        string workspaceId,
        UserIdentity user,
        ReportPatchDto patchDto)
    {
        var (oldStatus, oldResponsibleUserId) = await PreFetchStatusAndResponsibleAsync(scope, ct, reportId, patchDto);

        // Снимок `is_excluded_from_analytics` ДО UPDATE и под FOR UPDATE row lock
        // (см. tx-overload GetIsExcludedFromAnalyticsAsync). Нужен и чтобы эмиттер
        // увидел старое значение (после UPDATE SELECT вернул бы уже новое),
        // и чтобы сериализовать concurrent PATCH-toggle на одну строку —
        // второй PATCH дождётся первой tx и прочитает уже обновлённое значение,
        // правильно решив no-op vs emit.
        bool? oldIsExcluded = patchDto.IsExcludedFromAnalytics.HasValue
            ? await reportsDbClient.GetIsExcludedFromAnalyticsAsync(scope, reportId, ct)
            : null;

        var effectivePatch = ApplyAutoStatusDriver(patchDto, oldStatus, oldResponsibleUserId);

        var patchResult = await reportsDbClient.PatchReportAsync(reportId, effectivePatch, scope, ct);

        if (oldStatus.HasValue)
        {
            var statusEvt = ReportStatusEventFactory.TryCreate(
                workspaceId: workspaceId,
                reportId: reportId,
                fromStatus: (ReportStatus)oldStatus.Value,
                toStatus: (ReportStatus)patchResult.Status,
                actorUserId: user.Id);

            if (statusEvt is not null)
            {
                await domainEventPublisher.PublishAsync(statusEvt, scope, ct);
            }
        }

        if (patchDto.IsExcludedFromAnalytics.HasValue && oldIsExcluded.HasValue)
        {
            var excludedEvt = ReportExcludedEventFactory.TryCreate(
                workspaceId: workspaceId,
                reportId: reportId,
                oldIsExcluded: oldIsExcluded.Value,
                newIsExcluded: patchDto.IsExcludedFromAnalytics.Value,
                actorUserId: user.Id);

            if (excludedEvt is not null)
            {
                await domainEventPublisher.PublishAsync(excludedEvt, scope, ct);
            }
        }

        return (patchResult, effectivePatch);
    }

    private async Task<(int? OldStatus, string? OldResponsibleUserId)> PreFetchStatusAndResponsibleAsync(
        ITransactionScope scope,
        CancellationToken ct,
        int reportId,
        ReportPatchDto patchDto)
    {
        var needsPreFetch = patchDto.Status.HasValue || patchDto.ResponsibleUserId != null;
        if (!needsPreFetch)
        {
            return (null, null);
        }

        var current = await reportsDbClient.GetStatusAndResponsibleAsync(scope, reportId, ct);
        return current.HasValue
            ? (current.Value.Status, current.Value.ResponsibleUserId)
            : (null, null);
    }

    /// <summary>
    /// Если PATCH меняет responsible_user_id и явный <see cref="ReportPatchDto.Status"/>
    /// не задан, подставляет новый status согласно правилам перехода
    /// Backlog→Fix, Fix→Test, Test→Fix. Первый назначенный ответственный начинает фиксить,
    /// последующая передача — toggle между Fix и Test («отдали тестеру → нашли регрессию →
    /// починили»). Manual override (явный Status в PATCH) приоритетен и возвращается
    /// без изменений; Resolved/Rejected — терминалы, не пересчитываются.
    /// </summary>
    internal static ReportPatchDto ApplyAutoStatusDriver(
        ReportPatchDto patchDto,
        int? oldStatus,
        string? oldResponsibleUserId)
    {
        if (patchDto.Status.HasValue)
        {
            return patchDto;
        }

        if (patchDto.ResponsibleUserId == null)
        {
            return patchDto;
        }

        if (!oldStatus.HasValue)
        {
            return patchDto;
        }

        if (string.Equals(patchDto.ResponsibleUserId, oldResponsibleUserId, StringComparison.Ordinal))
        {
            return patchDto;
        }

        int? targetStatus = (ReportStatus)oldStatus.Value switch
        {
            ReportStatus.Backlog => (int)ReportStatus.Fix,
            ReportStatus.Test => (int)ReportStatus.Fix,
            ReportStatus.Fix => (int)ReportStatus.Test,
            _ => null,
        };

        if (targetStatus is null)
        {
            return patchDto;
        }

        return new ReportPatchDto
        {
            Title = patchDto.Title,
            Status = targetStatus,
            ResponsibleUserId = patchDto.ResponsibleUserId,
            IsExcludedFromAnalytics = patchDto.IsExcludedFromAnalytics,
        };
    }

    public async Task<(Report? Value, Error? Error)> GetReportAsync(string aliasId, string? organizationId, string? teamId)
    {
        var (reportId, publicId, teamReportId) = ReportIdResolveHelper.ResolveReportId(aliasId, aliasOptions.Value);
        var resolvedReport = await reportsDbClient.ResolveReportIdAsync(
            organizationId,
            teamId,
            reportId,
            publicId,
            teamReportId
        );
        if (resolvedReport == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        var report = await reportsDbClient.GetReportInternalAsync(resolvedReport.Id);
        if (report == null)
        {
            return (null, BoErrors.ReportNotFoundError);
        }

        return (ApplyBoSort(report), null);
    }

    public Task<ResolvedReportId?> ResolveReportIdAsync(
        string? organizationId,
        string? teamId,
        int? reportId,
        Guid? publicId,
        int? teamReportId)
    {
        return reportsDbClient.ResolveReportIdAsync(organizationId, teamId, reportId, publicId, teamReportId);
    }

    public Task<(long Total, Report[] Reports)> ListReportsAsync(string? organizationId, string? userId, string? teamId, int[]? reportStatuses, int[]? creatorTypes, int skip, int take)
    {
        return reportsDbClient.ListReportsAsync(organizationId, userId, teamId, reportStatuses, creatorTypes, skip, take);
    }

    public Task<(long Total, Report[] Reports)> SearchReportsAsync(SearchReports search)
    {
        return reportsDbClient.SearchReportsAsync(search);
    }

    public async Task<long[]> CountReportsBatchAsync(
        string? organizationId,
        IReadOnlyList<ReportCountsScopeDto> scopes,
        CancellationToken ct = default)
    {
        var counts = new long[scopes.Count];
        for (var i = 0; i < scopes.Count; i++)
        {
            var s = scopes[i];
            counts[i] = await reportsDbClient.CountReportsAsync(
                organizationId,
                s.TeamId,
                s.Statuses,
                s.CreatorTypes,
                ct);
        }
        return counts;
    }

    private static Report ApplyBoSort(Report report)
    {
        report.Bugs = report.Bugs?
        .OrderBy(b => b.Status switch
        {
            (int)BugStatus.Fixed => 0,
            (int)BugStatus.Open => 1,
            (int)BugStatus.Verified => 2,
            _ => 3,
        })
        .ThenBy(b => b.CreatedAt)
        .ToArray();

        foreach (var bug in report.Bugs ?? [])
        {
            bug.Attachments = bug.Attachments?.OrderBy(a => a.CreatedAt).ToArray();

            bug.Comments = bug.Comments?.OrderBy(c => c.CreatedAt).ToArray();
            foreach (var comment in bug.Comments ?? [])
            {
                comment.Attachments = comment.Attachments?.OrderBy(a => a.CreatedAt).ToArray();
            }

            bug.Steps = bug.Steps?.OrderBy(s => s.StepNumber).ToArray();
            foreach (var step in bug.Steps ?? [])
            {
                step.Attachments = step.Attachments?.OrderBy(a => a.CreatedAt).ToArray();
            }
        }

        report.Links = report.Links?.OrderBy(l => l.CreatedAt).ToArray();

        return report;
    }
}
