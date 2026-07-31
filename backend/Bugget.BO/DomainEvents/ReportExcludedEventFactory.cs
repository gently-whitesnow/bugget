using System.Text.Json;
using Bugget.Entities.BO.DomainEvents;

namespace Bugget.BO.DomainEvents;

/// <summary>
/// Формирует <see cref="DomainEvent"/> для события
/// <c>bugget.report.excluded_from_analytics_toggled</c> и инкапсулирует инвариант
/// дедупликации «не пишем event, если old == new».
/// </summary>
public static class ReportExcludedEventFactory
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Возвращает готовый event для публикации, либо <c>null</c>, если
    /// <paramref name="oldIsExcluded"/> == <paramref name="newIsExcluded"/>.
    /// </summary>
    public static DomainEvent? TryCreate(
        string workspaceId,
        int reportId,
        bool oldIsExcluded,
        bool newIsExcluded,
        string? actorUserId,
        short? actorCreatorType = null,
        DateTimeOffset? occurredAt = null,
        Guid? correlationId = null)
    {
        if (oldIsExcluded == newIsExcluded)
        {
            return null;
        }

        var payload = JsonSerializer.Serialize(
            new ReportExcludedFromAnalyticsToggledPayload
            {
                IsExcluded = newIsExcluded,
            },
            PayloadJsonOptions);

        return new DomainEvent
        {
            WorkspaceId = workspaceId,
            AggregateType = BuggetAggregateTypes.Report,
            AggregateId = reportId.ToString(),
            EventType = BuggetEventTypes.ReportExcludedFromAnalyticsToggled,
            Payload = payload,
            ActorUserId = actorUserId,
            ActorCreatorType = actorCreatorType,
            OccurredAt = occurredAt ?? DateTimeOffset.UtcNow,
            CorrelationId = correlationId ?? Guid.NewGuid(),
        };
    }
}
