using Bugget.Application.Results.Reports;

namespace Bugget.Application.Results;

public sealed class ReportViews
{
    public required long Total { get; init; }
    public required ReportViewModel[] Reports { get; init; }
}
