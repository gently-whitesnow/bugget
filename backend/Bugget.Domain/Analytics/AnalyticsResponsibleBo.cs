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

public sealed class ResponsibleParticipatedReportBo
{
    public required long ReportId { get; init; }
    public required string Title { get; init; }
    /// <summary>
    /// Текущая фаза репорта; ограничена Test/Fix — активные репорты, в которых
    /// пользователь участвует, всегда в одной из этих двух фаз.
    /// </summary>
    public required short CurrentPhase { get; init; }
}

public sealed class ResponsibleCompletedReportBo
{
    public required long ReportId { get; init; }
    public required string Title { get; init; }
    public required DateTimeOffset ClosedAt { get; init; }
    /// <summary>Финальный статус: <c>Resolved</c> / <c>Rejected</c>.</summary>
    public required short Outcome { get; init; }
}
