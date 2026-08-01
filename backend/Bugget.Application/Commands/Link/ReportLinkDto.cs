using System.ComponentModel.DataAnnotations;

namespace Bugget.Application.Commands.Link;

public sealed class ReportLinkDto
{
    [StringLength(2048, MinimumLength = 1)]
    public required string Link { get; init; }

    [StringLength(256, MinimumLength = 1)]
    public required string Name { get; init; }
}
