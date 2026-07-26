namespace Bugget.Entities.BO.Analytics;

/// <summary>
/// Снимок распределения багов репорта по статусам (для <c>AnalyticsReport</c>).
/// </summary>
public sealed class BugsByStatusBo
{
    public int Open { get; init; }
    public int Fixed { get; init; }
    public int Verified { get; init; }
    public int Rejected { get; init; }
}
