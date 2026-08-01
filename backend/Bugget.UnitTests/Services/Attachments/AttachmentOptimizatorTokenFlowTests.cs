using Bugget.Application.Interfaces;
using Bugget.Application.Ports;
using Bugget.Application.Services.Attachments;
using Bugget.Domain.Attachments;
using Bugget.Domain.Reports;
using Bugget.Infrastructure.Attachments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Bugget.UnitTests.Services.Attachments;

/// <summary>
/// Поведенческая половина регрессии на сквозную отмену: мало объявить параметр —
/// токен очереди обязан доехать до портов и до видеозаписи, иначе остановка приложения
/// не останавливает фоновую оптимизацию (MAIN-240).
/// </summary>
[Collection(VideoOptimizationCollection.Name)]
public sealed class AttachmentOptimizatorTokenFlowTests
{
    private static readonly Attachment TempVideo = new()
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
        StorageKind = (int)StorageKind.Temp,
    };

    [Fact(DisplayName = "Отменённый токен очереди останавливает оптимизацию до чтения оригинала")]
    public async Task Canceled_queue_token_stops_optimization()
    {
        var storage = new Mock<IFileStorageClient>();
        storage
            .Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult<Stream>(new MemoryStream(new byte[17]));
            });

        var db = new Mock<IAttachmentDbClient>();
        var optimizator = BuildOptimizator(storage.Object, db.Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            optimizator.OptimizeAttachmentAsync("org", new ReportIdContext(7, "alias", null), TempVideo, cts.Token));

        db.Verify(d => d.UpdateAttachmentAsync(It.IsAny<AttachmentUpdate>()), Times.Never);
    }

    [Fact(DisplayName = "Уже обработанное вложение не читается и не переписывается повторно")]
    public async Task Already_optimized_attachment_is_skipped()
    {
        var storage = new Mock<IFileStorageClient>();
        var db = new Mock<IAttachmentDbClient>();
        var optimizator = BuildOptimizator(storage.Object, db.Object);
        var standard = new Attachment
        {
            Id = TempVideo.Id,
            EntityId = TempVideo.EntityId,
            AttachType = TempVideo.AttachType,
            CreatedAt = TempVideo.CreatedAt,
            CreatorUserId = TempVideo.CreatorUserId,
            FileName = TempVideo.FileName,
            MimeType = TempVideo.MimeType,
            LengthBytes = TempVideo.LengthBytes,
            StorageKey = "original/7/42.mov",
            StorageKind = (int)StorageKind.Standard,
        };

        await optimizator.OptimizeAttachmentAsync(
            "org", new ReportIdContext(7, "alias", null), standard, CancellationToken.None);

        storage.Verify(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        db.Verify(d => d.UpdateAttachmentAsync(It.IsAny<AttachmentUpdate>()), Times.Never);
    }

    [Fact(DisplayName = "Оригинал без известной длины логируется без степени сжатия")]
    public async Task Non_seekable_original_is_logged_without_compression_ratio()
    {
        var storage = new Mock<IFileStorageClient>();
        storage
            .Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ForwardOnlyStream(new byte[17]));
        storage
            .Setup(s => s.WriteAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        storage
            .Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var db = new Mock<IAttachmentDbClient>();
        db.Setup(d => d.UpdateAttachmentAsync(It.IsAny<AttachmentUpdate>())).ReturnsAsync(TempVideo);
        var optimizator = BuildOptimizator(storage.Object, db.Object);

        await optimizator.OptimizeAttachmentAsync(
            "org", new ReportIdContext(7, "alias", null), TempVideo, CancellationToken.None);

        db.Verify(d => d.UpdateAttachmentAsync(It.IsAny<AttachmentUpdate>()), Times.Once);
    }

    [Fact(DisplayName = "Обработчик события вложения принимает токен очереди")]
    public void Attachment_create_handler_accepts_runtime_token()
    {
        typeof(AttachmentEventsService)
            .GetMethod(nameof(AttachmentEventsService.HandleAttachmentCreateEventAsync))!
            .GetParameters()
            .Should()
            .Contain(parameter => parameter.ParameterType == typeof(CancellationToken));
    }

    private static AttachmentOptimizator BuildOptimizator(IFileStorageClient storage, IAttachmentDbClient db)
    {
        var keyGen = new Mock<IAttachmentKeyGenerator>();
        // Видеооптимизация выключена: настоящий ffmpeg этим тестам не нужен.
        var options = Microsoft.Extensions.Options.Options.Create(
            new OptimizatorSettings { VideoOptimizationEnabled = false });
        var metrics = new VideoOptimizationMetrics();

        return new AttachmentOptimizator(
            storage,
            new AttachmentOptimizer(
                new ImageOptimizeWriter(storage, keyGen.Object, options),
                new VideoOptimizeWriter(
                    storage,
                    keyGen.Object,
                    new FfmpegProcessRunner(
                        new FfmpegService(options, NullLogger<FfmpegService>.Instance),
                        metrics,
                        NullLogger<FfmpegProcessRunner>.Instance),
                    new VideoTranscodeGate(options, metrics),
                    options,
                    NullLogger<VideoOptimizeWriter>.Instance),
                new TextOptimizeWriter(storage, keyGen.Object)),
            db,
            NullLogger<AttachmentOptimizator>.Instance,
            Mock.Of<IReportPageHubClient>());
    }

    /// <summary>Поток без длины: так выглядит оригинал, приехавший из сетевого хранилища.</summary>
    private sealed class ForwardOnlyStream(byte[] content) : MemoryStream(content)
    {
        public override bool CanSeek => false;
    }
}
