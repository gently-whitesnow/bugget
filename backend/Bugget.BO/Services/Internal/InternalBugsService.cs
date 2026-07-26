using System.Text.Json;
using Bugget.BO.DomainEvents;
using Bugget.BO.Errors;
using Bugget.BO.Services.Idempotency;
using Bugget.DA.Interfaces;
using Bugget.DA.Transactions;
using Bugget.Entities.BO.Common;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.DbModels.DomainEvents;
using Bugget.Entities.DTO.Bug;
using Bugget.Entities.DTO.Internal;
using Monade;

namespace Bugget.BO.Services.Internal;

/// <summary>
/// POST /v2/_internal/bugs: создание Report+Bug от имени внешнего автора (TgBetaTester)
/// с idempotency по `Idempotency-Key`. Force `creator_type = TgBetaTester` на сервис-уровне —
/// caller не может переопределить (TECHSPEC §4.3.1, ADR-20260423-external-author-internal-api).
/// Успешный результат кэшируется на TTL=24h; ошибка не кэшируется (retry допустим).
/// </summary>
public sealed class InternalBugsService(
    IReportsDbClient reportsDbClient,
    IBugsDbClient bugsDbClient,
    IDomainEventPublisher domainEventPublisher,
    IdempotencyCacheService idempotencyCacheService,
    IUnitOfWork unitOfWork)
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);
    private const short CreatorTypeTgBetaTester = (short)CreatorType.TgBetaTester;

    public async Task<MonadeStruct<InternalCreateBugResponseDto>> CreateAsync(
        string idempotencyKey,
        InternalCreateBugRequestDto request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BoErrors.IdempotencyKeyRequired;
        }

        if (string.IsNullOrWhiteSpace(request.WorkspaceId))
        {
            return BoErrors.WorkspaceIdRequired;
        }

        return await unitOfWork.ExecuteAsync((scope, c) =>
            idempotencyCacheService.GetOrComputeInScopeAsync<InternalCreateBugResponseDto>(
                scope,
                idempotencyKey,
                IdempotencyTtl,
                innerCt => CreateInternalAsync(scope, request, innerCt),
                c),
            ct);
    }

    private async Task<MonadeStruct<InternalCreateBugResponseDto>> CreateInternalAsync(
        ITransactionScope scope,
        InternalCreateBugRequestDto request,
        CancellationToken ct)
    {
        int reportId;
        if (request.ReportId is int existingReportId)
        {
            var reportStatus = await reportsDbClient.GetStatusInternalAsync(scope, existingReportId, ct);
            if (reportStatus is null)
            {
                return BoErrors.ReportNotFoundError;
            }

            if (IsClosedStatus(reportStatus.Value))
            {
                return BoErrors.ReportClosedError;
            }

            reportId = existingReportId;
        }
        else
        {
            var title = ResolveReportTitle(request);
            var (resolvedTeamId, resolvedOrgId) = ResolveWorkspace(request.WorkspaceId);
            var reportSummary = await reportsDbClient.CreateReportAsync(
                scope,
                userId: request.CreatorUserId,
                teamId: resolvedTeamId,
                organizationId: resolvedOrgId,
                title: title,
                creatorType: CreatorTypeTgBetaTester);
            reportId = reportSummary.Id;
        }

        var bugDto = new BugDto
        {
            Title = request.Title,
            Receive = request.Receive,
            Expect = request.Expect,
        };
        var bug = await bugsDbClient.CreateBugAsync(
            scope,
            userId: request.CreatorUserId,
            reportId: reportId,
            bugDto: bugDto,
            creatorType: CreatorTypeTgBetaTester);

        var payload = JsonSerializer.Serialize(new
        {
            bugId = bug.Id,
            reportId,
            title = bug.Title,
            creatorType = bug.CreatorType,
            creatorUserId = bug.CreatorUserId,
        });

        await domainEventPublisher.PublishAsync(new DomainEventDbModel
        {
            WorkspaceId = request.WorkspaceId!,
            AggregateType = BuggetAggregateTypes.Bug,
            AggregateId = bug.Id.ToString(),
            EventType = BuggetEventTypes.BugCreated,
            Payload = payload,
            ActorUserId = request.CreatorUserId,
            ActorCreatorType = CreatorTypeTgBetaTester,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
        }, scope, ct);

        return new InternalCreateBugResponseDto { ReportId = reportId, BugId = bug.Id };
    }

    private static (string? teamId, string? organizationId) ResolveWorkspace(string workspaceId)
    {
        // `creator_organization_id` — старое имя workspace_id в схеме bugget; именно по нему
        // JWT-claim `organization_id` фильтрует `/v2/reports`. team_id остаётся пустым,
        // т.к. beta-репорт не привязан к конкретной команде внутри workspace.
        return (null, workspaceId);
    }

    private static bool IsClosedStatus(int status)
        => status == (int)ReportStatus.Resolved || status == (int)ReportStatus.Rejected;

    private static string? ResolveReportTitle(InternalCreateBugRequestDto request)
    {
        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            return request.Title;
        }

        if (string.IsNullOrWhiteSpace(request.Receive))
        {
            return null;
        }

        return request.Receive.Length <= 80 ? request.Receive : request.Receive[..80];
    }
}
