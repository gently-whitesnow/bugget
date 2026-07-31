using System.ComponentModel.DataAnnotations;

namespace Bugget.Contracts.Dto.BugStep;

public sealed class BugStepDto
{
    [StringLength(2048, MinimumLength = 1)]
    public required string Text { get; init; }
}
