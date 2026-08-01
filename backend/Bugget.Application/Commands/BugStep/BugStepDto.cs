using System.ComponentModel.DataAnnotations;

namespace Bugget.Application.Commands.BugStep;

public sealed class BugStepDto
{
    [StringLength(2048, MinimumLength = 1)]
    public required string Text { get; init; }
}
