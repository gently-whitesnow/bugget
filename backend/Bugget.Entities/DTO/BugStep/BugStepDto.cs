using System.ComponentModel.DataAnnotations;

namespace Bugget.Entities.DTO.BugStep;

public sealed class BugStepDto
{
    [StringLength(2048, MinimumLength = 1)]
    public required string Text { get; init; }
}
