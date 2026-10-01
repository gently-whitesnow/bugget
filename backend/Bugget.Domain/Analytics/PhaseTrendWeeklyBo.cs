namespace Bugget.Domain.Analytics;

public sealed class PhaseTrendWeeklyBo
{
    public required string IsoWeek { get; init; }
    public required double TestDays { get; init; }
    public required double FixDays { get; init; }
    public required int ReportsClosed { get; init; }
}
