using System.Diagnostics.Metrics;
using Bugget.BO.Services.Attachments;
using Bugget.Entities.BO.AttachmentBo;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Bugget.Tests.Services.Attachments;

/// <summary>
/// Потолок одновременных видеозадач — единственное, что держит RSS процесса в бюджете
/// (MAIN-188). Здесь он проверяется характеризацией: сколько бы задач ни пришло разом,
/// одновременно работает ровно столько, сколько разрешено, а остальные ждут и доезжают.
/// </summary>
[Collection(VideoOptimizationCollection.Name)]
public sealed class VideoTranscodeGateTests
{
    private static readonly string[] ExpectedMetricNames =
    [
        "bugget.video.optimize.queued",
        "bugget.video.optimize.active",
        "bugget.video.optimize.duration",
        "bugget.video.ffmpeg.peak_rss",
    ];

    private static VideoTranscodeGate Gate(int concurrency) =>
        new(Options.Create(new OptimizatorSettings { VideoMaxConcurrency = concurrency }),
            new VideoOptimizationMetrics());

    [Fact(DisplayName = "Безопасный профиль: одновременно работает не больше одной задачи")]
    public async Task Safe_profile_allows_single_active_job()
    {
        var gate = Gate(1);
        var active = 0;
        var maxActive = 0;
        var guard = new object();

        var jobs = Enumerable.Range(0, 8).Select(async _ =>
        {
            using var lease = await gate.AcquireAsync(CancellationToken.None);
            lock (guard)
            {
                active++;
                maxActive = Math.Max(maxActive, active);
            }

            await Task.Delay(20);

            lock (guard)
            {
                active--;
            }

            lease.Complete(VideoOptimizeOutcome.Success);
        });

        await Task.WhenAll(jobs);

        maxActive.Should().Be(1, "потолок в один ffmpeg — весь смысл безопасного профиля");
        active.Should().Be(0);
    }

    [Fact(DisplayName = "Очередь не теряется: все накопившиеся задачи доезжают по одной")]
    public async Task Queued_jobs_all_complete()
    {
        var gate = Gate(1);
        var done = 0;

        var jobs = Enumerable.Range(0, 16).Select(async _ =>
        {
            using var lease = await gate.AcquireAsync(CancellationToken.None);
            Interlocked.Increment(ref done);
            lease.Complete(VideoOptimizeOutcome.Success);
        });

        await Task.WhenAll(jobs);

        done.Should().Be(16);
    }

    [Fact(DisplayName = "Настроенная параллельность больше единицы соблюдается")]
    public async Task Configured_concurrency_is_respected()
    {
        var gate = Gate(2);
        gate.MaxConcurrency.Should().Be(2);

        using var first = await gate.AcquireAsync(CancellationToken.None);
        using var second = await gate.AcquireAsync(CancellationToken.None);

        var third = gate.AcquireAsync(CancellationToken.None);
        var finished = await Task.WhenAny(third, Task.Delay(200));

        finished.Should().NotBeSameAs(third, "третья задача обязана ждать освобождения слота");

        second.Dispose();
        (await third).Dispose();
    }

    [Fact(DisplayName = "Отмена в очереди не занимает слот")]
    public async Task Canceled_wait_does_not_consume_slot()
    {
        var gate = Gate(1);
        using var held = await gate.AcquireAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource();

        var queued = gate.AcquireAsync(cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);

        held.Dispose();
        using var next = await gate.AcquireAsync(CancellationToken.None);
        next.Should().NotBeNull();
    }

    [Fact(DisplayName = "Отмена очереди и следующий job обнуляют queued и active")]
    public async Task Metrics_return_to_zero_after_queued_cancellation_and_next_job()
    {
        using var listener = new MeterListener();
        var instruments = new List<Instrument>();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Name == VideoOptimizationMetrics.MeterName)
            {
                instruments.Add(instrument);
                listener.EnableMeasurementEvents(instrument);
            }
        };

        var queued = 0;
        var active = 0;
        var durationResults = new List<string>();
        listener.SetMeasurementEventCallback<int>((instrument, value, _, _) =>
        {
            if (instrument.Name == "bugget.video.optimize.queued")
            {
                queued += value;
            }
            else if (instrument.Name == "bugget.video.optimize.active")
            {
                active += value;
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
        {
            if (instrument.Name == "bugget.video.optimize.duration")
            {
                durationResults.Add(tags.ToArray().Single().Value!.ToString()!);
            }
        });
        listener.Start();

        using var metrics = new VideoOptimizationMetrics();
        var gate = new VideoTranscodeGate(
            Options.Create(new OptimizatorSettings { VideoMaxConcurrency = 1 }), metrics);
        using var held = await gate.AcquireAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource();

        var canceled = gate.AcquireAsync(cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        held.Complete(VideoOptimizeOutcome.Success);
        held.Dispose();

        using (var next = await gate.AcquireAsync(CancellationToken.None))
        {
            next.Complete(VideoOptimizeOutcome.Success);
        }

        queued.Should().Be(0);
        active.Should().Be(0);
        durationResults.Should().OnlyContain(result => result == "success");
        instruments.Should().OnlyContain(instrument => ExpectedMetricNames.Contains(instrument.Name));
    }
}
