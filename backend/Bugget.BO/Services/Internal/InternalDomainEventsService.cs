using System.Text.Json;
using Bugget.BO.Errors;
using Bugget.DA.Interfaces;
using Bugget.Entities.DTO.Internal;
using Monade;

namespace Bugget.BO.Services.Internal;

/// <summary>
/// Consumer API для outbox <c>domain_events</c> (TECHSPEC §5.2, ADR-20260423-beta-bot-domain-events-outbox):
/// <list type="bullet">
///   <item>GET <c>/v2/_internal/domain-events</c> — cursor-based pull по workspace;</item>
///   <item>GET <c>/v2/_internal/domain-events/latest-id</c> — инициализация cursor
///     при первом открытии беты (возвращает <c>max(id)</c> или 0).</item>
/// </list>
/// </summary>
public sealed class InternalDomainEventsService(IDomainEventsDbClient domainEventsDbClient)
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 500;

    public async Task<MonadeStruct<InternalDomainEventsListResponseDto>> ListAsync(
        string? workspaceId,
        long? sinceId,
        int? limit,
        string? eventTypes,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return BoErrors.WorkspaceIdRequired;
        }

        if (sinceId is null)
        {
            return BoErrors.SinceIdRequired;
        }

        var resolvedLimit = limit is null
            ? DefaultLimit
            : Math.Clamp(limit.Value, 1, MaxLimit);

        var filterTypes = ParseEventTypes(eventTypes);

        var rows = await domainEventsDbClient.ListAsync(
            workspaceId, sinceId.Value, resolvedLimit, filterTypes, ct);

        var items = rows.Select(r => new InternalDomainEventItemDto
        {
            Id = r.Id,
            WorkspaceId = r.WorkspaceId,
            AggregateType = r.AggregateType,
            AggregateId = r.AggregateId,
            EventType = r.EventType,
            EventVersion = r.EventVersion,
            Payload = JsonSerializer.Deserialize<JsonElement>(r.Payload),
            ActorUserId = r.ActorUserId,
            ActorCreatorType = r.ActorCreatorType,
            OccurredAt = r.OccurredAt,
            CorrelationId = r.CorrelationId,
        }).ToArray();

        long? nextSinceId = items.Length == resolvedLimit ? items[^1].Id : null;

        return new InternalDomainEventsListResponseDto
        {
            Items = items,
            NextSinceId = nextSinceId,
        };
    }

    public async Task<MonadeStruct<InternalDomainEventLatestIdResponseDto>> GetLatestIdAsync(
        string? workspaceId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return BoErrors.WorkspaceIdRequired;
        }

        var latestId = await domainEventsDbClient.GetLatestIdAsync(workspaceId, ct);
        return new InternalDomainEventLatestIdResponseDto { LatestId = latestId };
    }

    private static string[]? ParseEventTypes(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return null;
        }

        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : parts;
    }
}
