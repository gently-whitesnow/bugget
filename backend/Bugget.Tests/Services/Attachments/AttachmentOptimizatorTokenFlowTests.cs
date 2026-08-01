using Bugget.BO.Interfaces;
using Bugget.BO.Ports;
using Bugget.BO.Services.Attachments;
using Bugget.Entities.BO.AttachmentBo;
using Bugget.Entities.BO.ReportBo;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Bugget.Tests.Services.Attachments;

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
        var options = Microsoft.Extensions.Options.Options.Create(new OptimizatorSettings());
        var metrics = new VideoOptimizationMetrics();

        return new AttachmentOptimizator(
            storage,
            new TextOptimizeWriter(storage, keyGen.Object),
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
            db,
            NullLogger<AttachmentOptimizator>.Instance,
            Mock.Of<IReportPageHubClient>());
    }
}
