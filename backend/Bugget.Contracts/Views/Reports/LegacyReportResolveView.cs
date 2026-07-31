namespace Bugget.Contracts.Views.Reports;

public sealed class LegacyReportResolveView
{
    public required string TeamId { get; init; }
    public required int TeamReportId { get; init; }
}
