using System.Text.Json;

namespace Bugget.Entities.DTO.Internal;

/// <summary>
/// Строка outbox, отдаваемая консьюмеру (beta-bot). Payload — уже распаршенный
/// JSON, чтобы консьюмер не парсил строку вручную.
/// </summary>
public sealed class InternalDomainEventItemDto
{
    public required long Id { get; init; }
    public required string WorkspaceId { get; init; }
    public required string AggregateType { get; init; }
    public required string AggregateId { get; init; }
    public required string EventType { get; init; }
    public required short EventVersion { get; init; }
    public required JsonElement Payload { get; init; }
    public string? ActorUserId { get; init; }
    public short? ActorCreatorType { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public Guid? CorrelationId { get; init; }
}
