using System.Text.Json;
using Bugget.Entities.BO.DomainEvents;
using Bugget.Entities.BO.ReportBo;

namespace Bugget.BO.DomainEvents;

/// <summary>
/// Формирует <see cref="DomainEvent"/> для события
/// <c>bugget.report.status_changed</c> и инкапсулирует инвариант дедупликации
/// «не пишем event, если from == to».
/// </summary>
public static class ReportStatusEventFactory
{
    /// <summary>
    /// Возвращает готовый event для публикации, либо <c>null</c>, если
    /// <paramref name="fromStatus"/> == <paramref name="toStatus"/>.
    /// </summary>
    public static DomainEvent? TryCreate(
        string workspaceId,
        int reportId,
        ReportStatus fromStatus,
        ReportStatus toStatus,
        string? actorUserId,
        short? actorCreatorType = null,
        DateTimeOffset? occurredAt = null,
        Guid? correlationId = null)
    {
        if (fromStatus == toStatus)
        {
            return null;
        }

        var payload = JsonSerializer.Serialize(new ReportStatusChangedPayload
        {
            FromStatus = fromStatus.ToString(),
            ToStatus = toStatus.ToString(),
        });

        return new DomainEvent
        {
            WorkspaceId = workspaceId,
            AggregateType = BuggetAggregateTypes.Report,
            AggregateId = reportId.ToString(),
            EventType = BuggetEventTypes.ReportStatusChanged,
            Payload = payload,
            ActorUserId = actorUserId,
            ActorCreatorType = actorCreatorType,
            OccurredAt = occurredAt ?? DateTimeOffset.UtcNow,
            CorrelationId = correlationId ?? Guid.NewGuid(),
        };
    }
}
