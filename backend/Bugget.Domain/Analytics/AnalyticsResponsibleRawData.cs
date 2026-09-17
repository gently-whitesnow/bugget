namespace Bugget.Domain.Analytics;

/// <summary>
/// Сырой снимок данных <c>/v2/analytics/responsible/{userId}</c>.
/// Сборка в <see cref="AnalyticsResponsibleBo"/> идёт в <c>AnalyticsService</c>.
/// </summary>
public sealed class AnalyticsResponsibleRawData
{
    public required IReadOnlyList<ResponsibleParticipatedReportBo> Participated { get; init; }
    public required IReadOnlyList<ResponsibleCompletedReportBo> Completed { get; init; }
    public double? AvgFixPhaseDays { get; init; }
}
