using System.ComponentModel.DataAnnotations;

namespace Bugget.Entities.DTO.Internal;

public sealed class InternalCreateBugStepRequestDto
{
    [Required]
    public required string CreatorUserId { get; init; }

    [Required]
    [StringLength(2048, MinimumLength = 1)]
    public required string Text { get; init; }

    [Range(1, int.MaxValue)]
    public int StepNumber { get; init; }
}
