using Bugget.Contracts.Views.Reports;

namespace Bugget.Contracts.Views;

public sealed class ReportViews
{
    public required long Total { get; init; }
    public required ReportViewModel[] Reports { get; init; }
}
