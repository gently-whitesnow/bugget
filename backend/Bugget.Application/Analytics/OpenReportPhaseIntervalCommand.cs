namespace Bugget.Application.Analytics;

/// <summary>
/// Команда прикладного слоя: открыть интервал фазы репорта в проекции аналитики.
///
/// Проекцию строит <c>ReportPhaseProjectionHandler</c> из событий
/// <c>bugget.report.status_changed</c>. Обработка событий идемпотентна:
/// <see cref="SourceEventId"/> — идентификатор породившего события, и повторная
/// доставка того же события не создаёт второй интервал.
/// </summary>
public sealed class OpenReportPhaseIntervalCommand
{
    /// <summary>Репорт, для которого открывается интервал.</summary>
    public required int ReportId { get; init; }

    /// <summary>Фаза жизненного цикла репорта, в которую он перешёл.</summary>
    public required short Phase { get; init; }

    /// <summary>Момент входа в фазу — время события, а не время обработки.</summary>
    public required DateTimeOffset EnteredAt { get; init; }

    /// <summary>Номер захода в фазу: 0 — первичный, 1 — первый повтор и так далее.</summary>
    public required int RegressionCycleIndex { get; init; }

    /// <summary>Событие-источник; по нему обработка события остаётся идемпотентной.</summary>
    public required long SourceEventId { get; init; }
}
