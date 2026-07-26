namespace Bugget.Entities.DTO.Internal;

public sealed class InternalDomainEventsListResponseDto
{
    public required IReadOnlyList<InternalDomainEventItemDto> Items { get; init; }
    public long? NextSinceId { get; init; }
}
