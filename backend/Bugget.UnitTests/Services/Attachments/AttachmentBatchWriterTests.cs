using System.Security.Claims;
using Bugget.Application.Errors;
using Bugget.Application.Interfaces;
using Bugget.Application.Ports;
using Bugget.Application.Services.Attachments;
using Bugget.Domain;
using Bugget.Domain.Attachments;
using Bugget.Domain.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Bugget.UnitTests.Services.Attachments;

public class AttachmentBatchWriterTests
{
    private static readonly AttachmentBatchTarget Target = new(
        new UserIdentity(new ClaimsPrincipal(new ClaimsIdentity())),
        ReportId: 1,
        AttachType.Comment);

    private static readonly Attachment SavedAttachment = new()
    {
        Id = 1,
        AttachType = (int)AttachType.Comment,
        CreatedAt = DateTimeOffset.UnixEpoch,
        CreatorUserId = "user",
        FileName = "first.txt",
    };

    [Fact(DisplayName = "Сбой записи в БД удаляет уже сохранённые файлы пакета")]
    public async Task DbFailureDiscardsWrittenFiles()
    {
        var storage = new Mock<IFileStorageClient>();
        var db = new Mock<IAttachmentDbClient>();
        db.SetupSequence(x => x.CreateAttachmentAsync(It.IsAny<ITransactionScope>(), It.IsAny<AttachmentCreate>()))
            .ReturnsAsync(SavedAttachment)
            .ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => CreateWriter(storage, db).CreateAsync(
            Target,
            [Upload("first.txt"), Upload("second.txt")],
            (_, _) => Task.FromResult(("owner", 7)),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        storage.Verify(x => x.WriteAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        storage.Verify(x => x.DeleteAsync("key-1", It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(x => x.DeleteAsync("key-2", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Пакет проверяется целиком до создания записи")]
    public void ValidateReportsFirstRejectedFile()
    {
        AttachmentBatchWriter.Validate([]).Should().Be(BoErrors.AttachmentFileNotSelectedOrEmpty);
        AttachmentBatchWriter.Validate([Upload("ok.txt"), Upload("noext")])
            .Should().Be(BoErrors.AttachmentFileExtensionNotFound);
        AttachmentBatchWriter.Validate(Enumerable.Repeat(Upload("ok.txt"), 11).ToArray())
            .Should().Be(BoErrors.AttachmentLimitExceeded);
        AttachmentBatchWriter.Validate([Upload("ok.txt")]).Should().BeNull();
    }

    private static AttachmentUpload Upload(string fileName) =>
        new(new MemoryStream([1]), new FileMeta(fileName, 1, "text/plain"));

    private static AttachmentBatchWriter CreateWriter(Mock<IFileStorageClient> storage, Mock<IAttachmentDbClient> db)
    {
        var scope = new Mock<ITransactionScope>().Object;
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(x => x.ExecuteAsync(It.IsAny<Func<ITransactionScope, CancellationToken, Task<(string, Attachment[])>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<ITransactionScope, CancellationToken, Task<(string, Attachment[])>>, CancellationToken>(
                (action, ct) => action(scope, ct));

        var keyNumber = 0;
        var keyGen = new Mock<IAttachmentKeyGenerator>();
        keyGen.Setup(x => x.GetOriginalKey(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .Returns(() => $"key-{++keyNumber}");
        keyGen.Setup(x => x.GetTempKey(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .Returns(() => $"key-{++keyNumber}");

        return new AttachmentBatchWriter(
            db.Object,
            storage.Object,
            keyGen.Object,
            unitOfWork.Object,
            attachmentEventsService: null!,
            NullLogger<AttachmentBatchWriter>.Instance);
    }
}
