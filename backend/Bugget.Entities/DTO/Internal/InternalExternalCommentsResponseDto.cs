namespace Bugget.Entities.DTO.Internal;

public sealed class InternalExternalCommentsResponseDto
{
    public required IReadOnlyList<InternalExternalCommentItemDto> Items { get; init; }
    public int? NextSinceId { get; init; }
}
