namespace Bugget.Domain.Analytics;

public sealed class TopRegressionReportBo
{
    public required long ReportId { get; init; }
    public required string Title { get; init; }
    public required int RegressionCycles { get; init; }
}
