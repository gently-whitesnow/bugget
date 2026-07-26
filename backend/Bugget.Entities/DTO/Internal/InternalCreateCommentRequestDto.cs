using System.ComponentModel.DataAnnotations;

namespace Bugget.Entities.DTO.Internal;

public sealed class InternalCreateCommentRequestDto
{
    [Required]
    public required int CreatorType { get; init; }

    [Required]
    public required string CreatorUserId { get; init; }

    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public required string Text { get; init; }
}
