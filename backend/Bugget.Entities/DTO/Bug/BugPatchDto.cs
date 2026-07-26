using System.ComponentModel.DataAnnotations;

namespace Bugget.Entities.DTO.Bug;

public sealed class BugPatchDto
{
    [StringLength(128, MinimumLength = 1)]
    public string? Title { get; init; }
    [StringLength(2048, MinimumLength = 1)]
    public string? Receive { get; init; }
    [StringLength(2048, MinimumLength = 1)]
    public string? Expect { get; init; }
    [Range(0, 3)]
    public int? Status { get; init; }
}
