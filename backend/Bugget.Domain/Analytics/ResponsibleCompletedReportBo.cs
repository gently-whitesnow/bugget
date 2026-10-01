namespace Bugget.Domain.Analytics;

public sealed class ResponsibleCompletedReportBo
{
    public required long ReportId { get; init; }
    public required string Title { get; init; }
    public required DateTimeOffset ClosedAt { get; init; }
    /// <summary>Финальный статус: <c>Resolved</c> / <c>Rejected</c>.</summary>
    public required short Outcome { get; init; }
}
