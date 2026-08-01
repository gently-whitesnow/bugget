using Bugget.Domain.Reports;

namespace Bugget.Application.ExternalSearch.Models;

public sealed class ExternalSearchApply
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required ReportIdContext reportIdContext { get; init; }
}
