using Bugget.Entities.BO.ReportBo;

namespace Bugget.BO.ExternalSearch.Models;

public sealed class ExternalSearchApply
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required ReportIdContext reportIdContext { get; init; }
}
