namespace Bugget.Domain.DomainEvents;

public sealed class DomainEvent
{
    public long Id { get; init; }
    public required string WorkspaceId { get; init; }
    public required string AggregateType { get; init; }
    public required string AggregateId { get; init; }
    public required string EventType { get; init; }
    public short EventVersion { get; init; } = 1;
    public required string Payload { get; init; }
    public string? ActorUserId { get; init; }
    public short? ActorCreatorType { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public Guid? CorrelationId { get; init; }
}
