using Bugget.Entities.Views.Reports;

namespace Bugget.Entities.Views;

public sealed class ReportViews
{
    public required long Total { get; init; }
    public required ReportViewModel[] Reports { get; init; }
}
