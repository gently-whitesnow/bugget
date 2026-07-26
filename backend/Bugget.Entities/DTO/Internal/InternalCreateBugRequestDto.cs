using System.ComponentModel.DataAnnotations;

namespace Bugget.Entities.DTO.Internal;

public sealed class InternalCreateBugRequestDto
{
    [Required]
    public required string WorkspaceId { get; init; }

    public int? ReportId { get; init; }

    [Required]
    public required string CreatorUserId { get; init; }

    [StringLength(80, MinimumLength = 1)]
    public string? Title { get; init; }

    [Required]
    [StringLength(4000, MinimumLength = 10)]
    public required string Receive { get; init; }

    [Required]
    [StringLength(4000, MinimumLength = 10)]
    public required string Expect { get; init; }
}
