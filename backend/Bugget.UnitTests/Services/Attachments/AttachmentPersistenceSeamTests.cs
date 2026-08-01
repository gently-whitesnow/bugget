using Bugget.Application.Interfaces;
using Bugget.Application.Ports;
using Bugget.Application.Realtime;
using Bugget.Application.Services.Attachments;
using Bugget.Domain.Attachments;
using Bugget.Domain.Reports;
using Bugget.Infrastructure.Attachments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Bugget.UnitTests.Services.Attachments;

/// <summary>
/// Шов между отменяемой подготовкой и неотменяемой записью результата (MAIN-243).
/// Токен очереди обязан останавливать ожидание слота и ffmpeg, но не имеет права
/// оборвать цепочку «storage → БД → уведомление → удаление temp»: прерванная посередине,
/// она оставляет вложение с записанным файлом и строкой в БД на удалённый temp-ключ.
/// </summary>
[Collection(VideoOptimizationCollection.Name)]
public sealed class AttachmentPersistenceSeamTests
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

    [Fact(DisplayName = "Отмена на шве записи не рвёт storage/БД/уведомление/очистку temp")]
    public async Task Cancellation_at_the_seam_does_not_break_the_chain()
    {
        using var cts = new CancellationTokenSource();
        var storage = new Mock<IFileStorageClient>();
        storage
            .Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[17]));
        storage
            .Setup(s => s.WriteAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            // Остановка приложения приходит ровно в момент записи постоянного объекта.
            .Returns(() => cts.CancelAsync());
        storage
            .Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var db = new Mock<IAttachmentDbClient>();
        db
            .Setup(d => d.UpdateAttachmentAsync(It.IsAny<AttachmentUpdate>()))
            .ReturnsAsync(TempVideo);
        var hub = new Mock<IReportPageHubClient>();
        var optimizator = BuildOptimizator(storage.Object, db.Object, hub.Object);

        await optimizator.OptimizeAttachmentAsync(
            "org", new ReportIdContext(7, "alias", null), TempVideo, cts.Token);

        cts.IsCancellationRequested.Should().BeTrue("иначе тест не проверяет отмену на шве");
        db.Verify(d => d.UpdateAttachmentAsync(It.IsAny<AttachmentUpdate>()), Times.Once);
        hub.Verify(
            h => h.SendAttachmentChangedAsync(It.IsAny<string>(), It.IsAny<AttachmentSocketView>()),
            Times.Once);
        storage.Verify(s => s.DeleteAsync("temp/capture.MOV", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Изменяющие вызовы получают неотменяемый токен")]
    public async Task Mutating_calls_receive_a_token_that_cannot_be_canceled()
    {
        using var cts = new CancellationTokenSource();
        var storage = MutationTrackingStorage(out var mutationTokens);
        var db = new Mock<IAttachmentDbClient>();
        db.Setup(d => d.UpdateAttachmentAsync(It.IsAny<AttachmentUpdate>())).ReturnsAsync(TempVideo);
        var optimizator = BuildOptimizator(storage, db.Object, Mock.Of<IReportPageHubClient>());

        await optimizator.OptimizeAttachmentAsync(
            "org", new ReportIdContext(7, "alias", null), TempVideo, cts.Token);

        mutationTokens.Should().HaveCount(2, "постоянная запись и удаление temp-оригинала");
        mutationTokens.Should().OnlyContain(
            token => !token.CanBeCanceled,
            "shutdown token не должен доходить до изменяющих портов");
    }

    [Fact(DisplayName = "Отмена до начала записи не пускает изменяющий вызов ни у одного писателя")]
    public async Task Cancellation_before_the_seam_blocks_every_writer()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var storage = MutationTrackingStorage(out var mutationTokens);
        var options = DisabledOptions;
        var keyGen = KeyGen();

        var writes = new List<Func<Task>>
        {
            () => new VideoOptimizeWriter(
                    storage, keyGen, Runner(),
                    new VideoTranscodeGate(options, new VideoOptimizationMetrics()),
                    options, NullLogger<VideoOptimizeWriter>.Instance)
                .OptimizeWriteAsync("org", 7, TempVideo, new MemoryStream(new byte[17]), cts.Token),
            () => new TextOptimizeWriter(storage, keyGen)
                .OptimizeWriteAsync("org", 7, TempVideo, new MemoryStream(new byte[17]), cts.Token),
            () => new ImageOptimizeWriter(storage, keyGen, options)
                .OptimizeWriteAsync("org", 7, TempVideo, TinyImageAsync().Result, cts.Token),
        };

        foreach (var write in writes)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(write);
        }

        mutationTokens.Should().BeEmpty("отменённая подготовка не имеет права ничего записать");
    }

    /// <summary>Настоящая картинка: на мусорном потоке писатель упал бы раньше проверки токена.</summary>
    private static async Task<Stream> TinyImageAsync()
    {
        using var image = new Image<Rgba32>(1, 1);
        var stream = new MemoryStream();
        await image.SaveAsWebpAsync(stream);
        stream.Position = 0;
        return stream;
    }

    private static IOptions<OptimizatorSettings> DisabledOptions =>
        Options.Create(new OptimizatorSettings { VideoOptimizationEnabled = false });

    private static FfmpegProcessRunner Runner()
    {
        var options = Options.Create(new OptimizatorSettings());
        return new FfmpegProcessRunner(
            new FfmpegService(options, NullLogger<FfmpegService>.Instance),
            new VideoOptimizationMetrics(),
            NullLogger<FfmpegProcessRunner>.Instance);
    }

    private static IAttachmentKeyGenerator KeyGen()
    {
        var keyGen = new Mock<IAttachmentKeyGenerator>();
        keyGen
            .Setup(k => k.GetOriginalKey(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .Returns((string? _, int reportId, int entityId, string extension) =>
                $"original/{reportId}/{entityId}{extension}");
        keyGen.Setup(k => k.GetPreviewKey(It.IsAny<string>())).Returns((string key) => $"{key}.preview");
        return keyGen.Object;
    }

    /// <summary>Хранилище, которое запоминает токены всех изменяющих вызовов.</summary>
    private static IFileStorageClient MutationTrackingStorage(out List<CancellationToken> mutationTokens)
    {
        var tokens = new List<CancellationToken>();
        mutationTokens = tokens;

        var storage = new Mock<IFileStorageClient>();
        storage
            .Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[17]));
        storage
            .Setup(s => s.WriteAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Stream _, CancellationToken ct) =>
            {
                tokens.Add(ct);
                return Task.CompletedTask;
            });
        storage
            .Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken ct) =>
            {
                tokens.Add(ct);
                return Task.CompletedTask;
            });
        return storage.Object;
    }

    private static AttachmentOptimizator BuildOptimizator(
        IFileStorageClient storage,
        IAttachmentDbClient db,
        IReportPageHubClient hub)
    {
        var keyGen = KeyGen();
        var options = DisabledOptions;
        var metrics = new VideoOptimizationMetrics();

        return new AttachmentOptimizator(
            storage,
            new AttachmentOptimizer(
                new ImageOptimizeWriter(storage, keyGen, options),
                new VideoOptimizeWriter(
                    storage,
                    keyGen,
                    Runner(),
                    new VideoTranscodeGate(options, metrics),
                    options,
                    NullLogger<VideoOptimizeWriter>.Instance),
                new TextOptimizeWriter(storage, keyGen)),
            db,
            NullLogger<AttachmentOptimizator>.Instance,
            hub);
    }
}
