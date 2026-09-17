namespace Bugget.Domain.Analytics;

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
