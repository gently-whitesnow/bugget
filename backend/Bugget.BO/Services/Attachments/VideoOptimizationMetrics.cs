using System.Diagnostics.Metrics;

namespace Bugget.BO.Services.Attachments;

/// <summary>Чем закончилась фоновая оптимизация одного видео. Метка метрики — низкой кардинальности.</summary>
public enum VideoOptimizeOutcome
{
    Success,
    Failure,
    Timeout,
    Canceled,
    Disabled
}

/// <summary>
/// Приборы фоновой видеооптимизации. Отдельного эндпоинта нет: счётчики уезжают в
/// существующий OpenTelemetry-конвейер и скрейпятся тем же Prometheus, что и остальное.
/// Меток ровно одна и с конечным набором значений — иначе временной ряд разъедет.
/// </summary>
public sealed class VideoOptimizationMetrics : IDisposable
{
    public const string MeterName = "Bugget.VideoOptimization";

    private readonly Meter _meter = new(MeterName);
    private readonly UpDownCounter<int> _queued;
    private readonly UpDownCounter<int> _active;
    private readonly Histogram<double> _duration;
    private readonly Histogram<long> _peakChildRss;

    public VideoOptimizationMetrics()
    {
        _queued = _meter.CreateUpDownCounter<int>(
            "bugget.video.optimize.queued",
            unit: "{job}",
            description: "Задачи видеооптимизации, ожидающие слот ffmpeg.");
        _active = _meter.CreateUpDownCounter<int>(
            "bugget.video.optimize.active",
            unit: "{job}",
            description: "Задачи видеооптимизации, занявшие слот ffmpeg.");
        // Счётчик исходов отдельно не заводим: он выводится из _count гистограммы по метке result.
        _duration = _meter.CreateHistogram<double>(
            "bugget.video.optimize.duration",
            unit: "s",
            description: "Длительность работы под слотом ffmpeg, по исходу.");
        _peakChildRss = _meter.CreateHistogram<long>(
            "bugget.video.ffmpeg.peak_rss",
            unit: "By",
            description: "Пиковый RSS дочернего ffmpeg-процесса (только Linux).");
    }

    public void QueuedChanged(int delta) => _queued.Add(delta);

    public void ActiveChanged(int delta) => _active.Add(delta);

    public void RecordDuration(TimeSpan elapsed, VideoOptimizeOutcome outcome) =>
        _duration.Record(elapsed.TotalSeconds, new KeyValuePair<string, object?>("result", OutcomeTag(outcome)));

    public void RecordPeakChildRss(long bytes) => _peakChildRss.Record(bytes);

    private static string OutcomeTag(VideoOptimizeOutcome outcome) => outcome switch
    {
        VideoOptimizeOutcome.Success => "success",
        VideoOptimizeOutcome.Failure => "failure",
        VideoOptimizeOutcome.Timeout => "timeout",
        VideoOptimizeOutcome.Canceled => "canceled",
        VideoOptimizeOutcome.Disabled => "disabled",
        _ => "failure"
    };

    public void Dispose() => _meter.Dispose();
}
