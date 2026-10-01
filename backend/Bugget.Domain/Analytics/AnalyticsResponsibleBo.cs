namespace Bugget.Domain.Analytics;

/// <summary>
/// BO-сводка по конкретному ответственному пользователю
/// (<c>GET /v2/analytics/responsible/{userId}</c>).
/// </summary>
public sealed class AnalyticsResponsibleBo
{
    public required PeriodWindow Period { get; init; }
    public required IReadOnlyList<ResponsibleParticipatedReportBo> ReportsParticipated { get; init; }
    public required IReadOnlyList<ResponsibleCompletedReportBo> ReportsCompleted { get; init; }
    public double? AvgFixPhaseDays { get; init; }
}
