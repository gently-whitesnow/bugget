namespace Bugget.Application.Analytics;

/// <summary>
/// Открыть интервал фазы репорта в проекции аналитики (строится из <c>bugget.report.status_changed</c>).
/// Идемпотентно по <see cref="SourceEventId"/>: повторная доставка события второй интервал не создаёт.
/// </summary>
public sealed class OpenReportPhaseIntervalCommand
{
    public required int ReportId { get; init; }

    public required short Phase { get; init; }

    /// <summary>Момент входа в фазу — время события, а не время обработки.</summary>
    public required DateTimeOffset EnteredAt { get; init; }

    /// <summary>Номер захода в фазу: 0 — первичный, 1 — первый повтор и так далее.</summary>
    public required int RegressionCycleIndex { get; init; }

    public required long SourceEventId { get; init; }
}
