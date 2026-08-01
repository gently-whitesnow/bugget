using System.ComponentModel.DataAnnotations;
namespace Bugget.Application.Commands.Report;

public sealed class ReportCreateDto
{
    [StringLength(128, MinimumLength = 1)]
    public required string Title { get; init; }
}
