using Bugget.BO.Interfaces;
using Bugget.BO.Ports;
using Bugget.BO.Services.Attachments;
using Bugget.Entities.BO.AttachmentBo;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Bugget.Tests.Services.Attachments;

/// <summary>
/// Характеризация фоновой видеооптимизации: что она делает до того, как получила слот,
/// с какими потолками потоков зовёт ffmpeg и что происходит при выключенной оптимизации.
/// Настоящий ffmpeg здесь не нужен — до запуска процесса дело не доходит.
/// </summary>
[Collection(VideoOptimizationCollection.Name)]
public sealed class VideoOptimizeWriterTests
{
    private static readonly Attachment VideoAttachment = new()
    {
        Id = 1,
        EntityId = 42,
        AttachType = 0,
        CreatedAt = DateTimeOffset.UnixEpoch,
        CreatorUserId = "user",
        FileName = "capture.MOV",
        MimeType = "video/quicktime",
        LengthBytes = 17,
        StorageKey = "temp/capture.MOV",
    };

    [Fact(DisplayName = "До получения слота оригинал не копируется во временный файл")]
    public async Task Nothing_heavy_happens_before_the_gate()
    {
        var settings = new OptimizatorSettings { VideoMaxConcurrency = 1 };
        var gate = new VideoTranscodeGate(Options.Create(settings), new VideoOptimizationMetrics());
        var writer = BuildWriter(settings, gate, out var storage);

        // Единственный слот занят: следующая задача обязана ждать, ничего не подготавливая.
        using var occupied = await gate.AcquireAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource();
        await using var source = new ReadTrackingStream(new byte[64 * 1024]);

        var queued = writer.OptimizeWriteAsync("org", 7, VideoAttachment, source, cts.Token);
        await Task.Delay(200, CancellationToken.None);

        source.WasRead.Should().BeFalse("копирование оригинала обязано начаться только под слотом");
        queued.IsCompleted.Should().BeFalse();

        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        storage.Verify(
            s => s.WriteAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName = "Выключенная оптимизация отдаёт оригинал под постоянным ключом")]
    public async Task Disabled_optimization_stores_original_as_is()
    {
        var settings = new OptimizatorSettings { VideoOptimizationEnabled = false };
        var gate = new VideoTranscodeGate(Options.Create(settings), new VideoOptimizationMetrics());
        var writer = BuildWriter(settings, gate, out var storage);
        await using var source = new MemoryStream(new byte[17]);

        var result = await writer.OptimizeWriteAsync("org", 7, VideoAttachment, source, CancellationToken.None);

        result.StorageKey.Should().Be("original/7/42.mov");
        result.FileName.Should().Be("capture.MOV");
        result.MimeType.Should().Be("video/quicktime");
        result.HasPreview.Should().BeFalse();
        result.LengthBytes.Should().Be(17);
        storage.Verify(
            s => s.WriteAsync("original/7/42.mov", source, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Команда перекодирования несёт все три потолка потоков")]
    public void Transcode_arguments_carry_thread_caps()
    {
        var settings = new OptimizatorSettings
        {
            VideoDecoderThreads = 1,
            VideoEncoderThreads = 2,
            VideoFilterThreads = 3,
        };

        var arguments = VideoOptimizeWriter.BuildTranscodeArguments(settings, "/tmp/in.mov", "/tmp/out.mp4");
        var input = Array.IndexOf(arguments, "-i");

        // -threads до -i ограничивает декодер (самый дорогой по памяти на 4K),
        // -threads после — выходной кодировщик; -filter_threads глобальный.
        ValueAfterLast(arguments, "-threads", before: input).Should().Be("1");
        ValueAfterLast(arguments, "-threads", before: arguments.Length).Should().Be("2");
        ValueAfterLast(arguments, "-filter_threads", before: input).Should().Be("3");
        arguments.Should().EndWith(["/tmp/out.mp4"]);
    }

    [Fact(DisplayName = "Команда превью несёт те же потолки потоков")]
    public void Preview_arguments_carry_thread_caps()
    {
        var settings = new OptimizatorSettings
        {
            VideoDecoderThreads = 1,
            VideoEncoderThreads = 2,
            VideoFilterThreads = 3,
        };

        var arguments = VideoOptimizeWriter.BuildPreviewArguments(settings, "/tmp/out.mp4", "/tmp/preview.webp");
        var input = Array.IndexOf(arguments, "-i");

        ValueAfterLast(arguments, "-threads", before: input).Should().Be("1");
        ValueAfterLast(arguments, "-threads", before: arguments.Length).Should().Be("2");
        ValueAfterLast(arguments, "-filter_threads", before: input).Should().Be("3");
    }

    /// <summary>Значение последнего вхождения флага левее позиции <paramref name="before"/>.</summary>
    private static string ValueAfterLast(string[] arguments, string flag, int before)
    {
        var index = Array.LastIndexOf(arguments, flag, Math.Min(before, arguments.Length) - 1);
        index.Should().BeGreaterThanOrEqualTo(0, "флаг {0} обязан быть в команде", flag);
        return arguments[index + 1];
    }

    private static VideoOptimizeWriter BuildWriter(
        OptimizatorSettings settings,
        VideoTranscodeGate gate,
        out Mock<IFileStorageClient> storage)
    {
        storage = new Mock<IFileStorageClient>();
        storage
            .Setup(s => s.WriteAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var keyGen = new Mock<IAttachmentKeyGenerator>();
        keyGen
            .Setup(k => k.GetOriginalKey(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .Returns((string? _, int reportId, int entityId, string extension) =>
                $"original/{reportId}/{entityId}{extension}");
        keyGen.Setup(k => k.GetPreviewKey(It.IsAny<string>())).Returns((string key) => $"{key}.preview");

        var options = Options.Create(settings);
        var runner = new FfmpegProcessRunner(
            new FfmpegService(options, NullLogger<FfmpegService>.Instance),
            new VideoOptimizationMetrics(),
            NullLogger<FfmpegProcessRunner>.Instance);

        return new VideoOptimizeWriter(
            storage.Object,
            keyGen.Object,
            runner,
            gate,
            options,
            NullLogger<VideoOptimizeWriter>.Instance);
    }

    /// <summary>Поток, который помнит, читал ли его кто-нибудь: это и есть признак тяжёлой подготовки.</summary>
    private sealed class ReadTrackingStream(byte[] content) : MemoryStream(content)
    {
        public bool WasRead { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            WasRead = true;
            return base.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WasRead = true;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }
}
