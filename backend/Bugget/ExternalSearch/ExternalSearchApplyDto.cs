namespace Bugget.BO.ExternalSearch.Models;

public sealed class ExternalSearchApplyDto
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string ReportId { get; init; }
}
