using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Bugget.Infrastructure.Attachments;

/// <summary>
/// Единственный на процесс потолок одновременных видеозадач. Слот берётся до любой
/// тяжёлой подготовки — копирования оригинала во временный файл и запуска ffmpeg,
/// — иначе ожидающие задачи размножают временные копии и дочерние процессы, а
/// потолок памяти становится фиктивным (MAIN-188). Очередь не теряется: задача
/// просто ждёт слот и выполняется следом.
/// </summary>
public sealed class VideoTranscodeGate
{
    private readonly SemaphoreSlim _slots;
    private readonly VideoOptimizationMetrics _metrics;

    public VideoTranscodeGate(IOptions<OptimizatorSettings> options, VideoOptimizationMetrics metrics)
    {
        MaxConcurrency = options.Value.VideoMaxConcurrency;
        _slots = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        _metrics = metrics;
    }

    public int MaxConcurrency { get; }

    /// <summary>
    /// Ждёт свободный слот. Возвращённый лизинг обязан быть освобождён: до этого момента
    /// слот занят, а на <see cref="Lease.Dispose"/> пишется длительность и исход.
    /// </summary>
    public async Task<Lease> AcquireAsync(CancellationToken ct)
    {
        _metrics.QueuedChanged(1);
        try
        {
            await _slots.WaitAsync(ct);
        }
        finally
        {
            _metrics.QueuedChanged(-1);
        }

        _metrics.ActiveChanged(1);
        return new Lease(this);
    }

    private void Release() => _slots.Release();

    public sealed class Lease : IDisposable
    {
        private readonly VideoTranscodeGate _gate;
        private readonly Stopwatch _elapsed = Stopwatch.StartNew();
        private VideoOptimizeOutcome _outcome = VideoOptimizeOutcome.Failure;
        private bool _released;

        internal Lease(VideoTranscodeGate gate) => _gate = gate;

        /// <summary>Исход по умолчанию — Failure: незакрытая явно работа считается упавшей.</summary>
        public void Complete(VideoOptimizeOutcome outcome) => _outcome = outcome;

        public void Dispose()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            _elapsed.Stop();
            _gate._metrics.ActiveChanged(-1);
            _gate._metrics.RecordDuration(_elapsed.Elapsed, _outcome);
            _gate.Release();
        }
    }
}
