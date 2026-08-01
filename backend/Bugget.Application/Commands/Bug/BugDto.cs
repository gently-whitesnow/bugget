using System.ComponentModel.DataAnnotations;

namespace Bugget.Application.Commands.Bug;

public sealed class BugDto
{
    [StringLength(128, MinimumLength = 1)]
    public string? Title { get; init; }
    [StringLength(2048, MinimumLength = 1)]
    public string? Receive { get; init; }
    [StringLength(2048, MinimumLength = 1)]
    public string? Expect { get; init; }
}
