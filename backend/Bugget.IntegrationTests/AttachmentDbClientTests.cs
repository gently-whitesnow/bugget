using Bugget.Application.Ports;
using Bugget.Contracts.Dto.Bug;
using Bugget.Contracts.Dto.BugStep;
using Bugget.Contracts.Dto.Report;
using Bugget.Domain.Attachments;
using Bugget.Domain.Bugs;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class AttachmentDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IAttachmentDbClient _attachmentDbClient;
    private readonly IBugsDbClient _bugsDbClient;
    private readonly IBugStepsDbClient _bugStepsDbClient;
    private readonly ICommentsDbClient _commentsDbClient;
    private readonly IReportsDbClient _reportsDbClient;

    // AttachType константы (из документации/схемы БД)
    private const int AttachType_BugFact = 0;      // Вложение к receive бага
    private const int AttachType_BugExpected = 1;  // Вложение к expect бага
    private const int AttachType_Comment = 2;      // Вложение к комментарию
    private const int AttachType_BugStep = 3;      // Вложение к шагу бага

    public AttachmentDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _attachmentDbClient = scope.ServiceProvider.GetRequiredService<IAttachmentDbClient>();
        _bugsDbClient = scope.ServiceProvider.GetRequiredService<IBugsDbClient>();
        _bugStepsDbClient = scope.ServiceProvider.GetRequiredService<IBugStepsDbClient>();
        _commentsDbClient = scope.ServiceProvider.GetRequiredService<ICommentsDbClient>();
        _reportsDbClient = scope.ServiceProvider.GetRequiredService<IReportsDbClient>();
    }

    #region CreateAttachment Tests

    [Fact(DisplayName = "Успешное создание вложения для бага")]
    public async Task CreateAttachment_ForBug_ShouldCreateAttachment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var createModel = new AttachmentCreate
        {
            EntityId = bug.Id,
            AttachType = AttachType_BugFact,
            StorageKey = $"attachments/bug_{bug.Id}_{Guid.NewGuid()}.jpg",
            StorageKind = 1, // Standard
            CreatorUserId = userId,
            LengthBytes = 1024000,
            FileName = "test_image.jpg",
            MimeType = "image/jpeg"
        };

        // Act
        var result = await _attachmentDbClient.CreateAttachment(createModel);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(bug.Id, result.EntityId);
        Assert.Equal(AttachType_BugFact, result.AttachType);
        Assert.Equal(createModel.StorageKey, result.StorageKey);
        Assert.Equal(createModel.StorageKind, result.StorageKind);
        Assert.Equal(userId, result.CreatorUserId);
        Assert.Equal(createModel.LengthBytes, result.LengthBytes);
        Assert.Equal(createModel.FileName, result.FileName);
        Assert.Equal(createModel.MimeType, result.MimeType);
        Assert.True(result.CreatedAt > DateTimeOffset.MinValue);
    }

    [Fact(DisplayName = "Успешное создание вложения для комментария")]
    public async Task CreateAttachment_ForComment_ShouldCreateAttachment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, report.Id, bug.Id, "Test comment");
        var createModel = new AttachmentCreate
        {
            EntityId = comment.Id,
            AttachType = AttachType_Comment,
            StorageKey = $"attachments/comment_{comment.Id}_{Guid.NewGuid()}.pdf",
            StorageKind = 1,
            CreatorUserId = userId,
            LengthBytes = 512000,
            FileName = "document.pdf",
            MimeType = "application/pdf"
        };

        // Act
        var result = await _attachmentDbClient.CreateAttachment(createModel);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(comment.Id, result.EntityId);
        Assert.Equal(AttachType_Comment, result.AttachType);
        Assert.Equal(createModel.FileName, result.FileName);
        Assert.Equal(createModel.MimeType, result.MimeType);
    }

    [Fact(DisplayName = "Успешное создание вложения для шага бага")]
    public async Task CreateAttachment_ForBugStep_ShouldCreateAttachment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step = await CreateTestBugStepAsync(userId, report.Id, bug.Id);
        var createModel = new AttachmentCreate
        {
            EntityId = step.Id,
            AttachType = AttachType_BugStep,
            StorageKey = $"attachments/bug_step_{step.Id}_{Guid.NewGuid()}.jpg",
            StorageKind = 1,
            CreatorUserId = userId,
            LengthBytes = 1024000,
            FileName = "test_step_image.jpg",
            MimeType = "image/jpeg"
        };

        // Act
        var result = await _attachmentDbClient.CreateAttachment(createModel);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(step.Id, result.EntityId);
        Assert.Equal(AttachType_BugStep, result.AttachType);
        Assert.Equal(createModel.FileName, result.FileName);
    }

    [Fact(DisplayName = "Создание нескольких вложений для одного бага")]
    public async Task CreateAttachment_MultipleBugAttachments_ShouldCreateAll()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);

        var attachment1 = new AttachmentCreate
        {
            EntityId = bug.Id,
            AttachType = AttachType_BugFact,
            StorageKey = $"attachments/bug_{bug.Id}_1.jpg",
            StorageKind = 1,
            CreatorUserId = userId,
            LengthBytes = 1000,
            FileName = "file1.jpg",
            MimeType = "image/jpeg"
        };

        var attachment2 = new AttachmentCreate
        {
            EntityId = bug.Id,
            AttachType = AttachType_BugFact,
            StorageKey = $"attachments/bug_{bug.Id}_2.png",
            StorageKind = 1,
            CreatorUserId = userId,
            LengthBytes = 2000,
            FileName = "file2.png",
            MimeType = "image/png"
        };

        // Act
        var result1 = await _attachmentDbClient.CreateAttachment(attachment1);
        var result2 = await _attachmentDbClient.CreateAttachment(attachment2);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotEqual(result1.Id, result2.Id);
        Assert.Equal(bug.Id, result1.EntityId);
        Assert.Equal(bug.Id, result2.EntityId);
    }

    #endregion

    #region UpdateAttachment Tests

    [Fact(DisplayName = "Успешное обновление вложения")]
    public async Task UpdateAttachmentAsync_WithNewData_ShouldUpdateAttachment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var attachment = await CreateTestBugAttachmentAsync(userId, bug.Id);

        var updateModel = new AttachmentUpdate
        {
            Id = attachment.Id,
            StorageKey = "updated/storage/key.jpg",
            StorageKind = 2, // Cold storage
            LengthBytes = 2048000,
            FileName = "updated_filename.jpg",
            MimeType = "image/jpeg",
            HasPreview = true,
            IsGzipCompressed = true
        };

        // Act
        var result = await _attachmentDbClient.UpdateAttachmentAsync(updateModel);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(attachment.Id, result.Id);
        Assert.Equal(updateModel.StorageKey, result.StorageKey);
        Assert.Equal(updateModel.StorageKind, result.StorageKind);
        Assert.Equal(updateModel.LengthBytes, result.LengthBytes);
        Assert.Equal(updateModel.FileName, result.FileName);
        Assert.Equal(updateModel.MimeType, result.MimeType);
        Assert.Equal(updateModel.HasPreview, result.HasPreview);
        Assert.Equal(updateModel.IsGzipCompressed, result.IsGzipCompressed);
    }

    [Fact(DisplayName = "Обновление только имени файла")]
    public async Task UpdateAttachmentAsync_OnlyFileName_ShouldUpdate()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var attachment = await CreateTestBugAttachmentAsync(userId, bug.Id);

        var updateModel = new AttachmentUpdate
        {
            Id = attachment.Id,
            StorageKey = attachment.StorageKey!,
            StorageKind = attachment.StorageKind ?? 1,
            LengthBytes = attachment.LengthBytes ?? 0,
            FileName = "completely_new_name.jpg",
            MimeType = attachment.MimeType,
            HasPreview = attachment.HasPreview ?? false,
            IsGzipCompressed = attachment.IsGzipCompressed ?? false
        };

        // Act
        var result = await _attachmentDbClient.UpdateAttachmentAsync(updateModel);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("completely_new_name.jpg", result.FileName);
        Assert.Equal(attachment.StorageKey, result.StorageKey);
    }

    #endregion

    #region GetBugAttachment Tests

    [Fact(DisplayName = "Получение вложения бага по ID")]
    public async Task GetBugAttachmentAsync_ValidAttachment_ShouldReturnAttachment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var attachment = await CreateTestBugAttachmentAsync(userId, bug.Id);

        // Act
        var result = await _attachmentDbClient.GetBugAttachmentInternalAsync(report.Id, bug.Id, attachment.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(attachment.Id, result.Id);
        Assert.Equal(attachment.FileName, result.FileName);
    }

    [Fact(DisplayName = "Получение несуществующего вложения бага")]
    public async Task GetBugAttachmentAsync_NonExistent_ShouldReturnNull()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);

        // Act
        var result = await _attachmentDbClient.GetBugAttachmentInternalAsync(report.Id, bug.Id, 999999);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Получение вложения бага с organizationId")]
    public async Task GetBugAttachmentAsync_WithOrganization_ShouldReturnAttachment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var organizationId = $"org_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId, organizationId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var attachment = await CreateTestBugAttachmentAsync(userId, bug.Id);

        // Act
        var result = await _attachmentDbClient.GetBugAttachmentInternalAsync(report.Id, bug.Id, attachment.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(attachment.Id, result.Id);
    }

    #endregion

    #region GetCommentAttachment Tests

    [Fact(DisplayName = "Получение вложения комментария по ID")]
    public async Task GetCommentAttachmentAsync_ValidAttachment_ShouldReturnAttachment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, report.Id, bug.Id, "Test");
        var attachment = await CreateTestCommentAttachmentAsync(userId, comment.Id);

        // Act
        var result = await _attachmentDbClient.GetCommentAttachmentInternalAsync(
            report.Id, bug.Id, comment.Id, attachment.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(attachment.Id, result.Id);
        Assert.Equal(attachment.FileName, result.FileName);
    }

    [Fact(DisplayName = "Получение вложения шага бага по ID")]
    public async Task GetBugStepAttachmentAsync_ValidAttachment_ShouldReturnAttachment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step = await CreateTestBugStepAsync(userId, report.Id, bug.Id);
        var attachment = await CreateTestBugStepAttachmentAsync(userId, step.Id);

        // Act
        var result = await _attachmentDbClient.GetBugStepAttachmentInternalAsync(
            report.Id, bug.Id, step.Id, attachment.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(attachment.Id, result.Id);
        Assert.Equal(attachment.FileName, result.FileName);
    }

    [Fact(DisplayName = "Получение несуществующего вложения комментария")]
    public async Task GetCommentAttachmentAsync_NonExistent_ShouldReturnNull()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, report.Id, bug.Id, "Test");

        // Act
        var result = await _attachmentDbClient.GetCommentAttachmentInternalAsync(
            report.Id, bug.Id, comment.Id, 999999);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetBugAttachmentsCount Tests

    [Fact(DisplayName = "Подсчет вложений бага - нет вложений")]
    public async Task GetBugAttachmentsCountAsync_NoAttachments_ShouldReturnZero()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);

        // Act
        var count = await _attachmentDbClient.GetBugAttachmentsCountInternalAsync(report.Id, bug.Id, AttachType_BugFact);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact(DisplayName = "Подсчет вложений бага - несколько вложений")]
    public async Task GetBugAttachmentsCountAsync_MultipleAttachments_ShouldReturnCorrectCount()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);

        await CreateTestBugAttachmentAsync(userId, bug.Id);
        await CreateTestBugAttachmentAsync(userId, bug.Id);
        await CreateTestBugAttachmentAsync(userId, bug.Id);

        // Act
        var count = await _attachmentDbClient.GetBugAttachmentsCountInternalAsync(report.Id, bug.Id, AttachType_BugFact);

        // Assert
        Assert.Equal(3, count);
    }

    #endregion

    #region GetCommentAttachmentsCount Tests

    [Fact(DisplayName = "Подсчет вложений комментария - нет вложений")]
    public async Task GetCommentAttachmentsCountAsync_NoAttachments_ShouldReturnZero()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, report.Id, bug.Id, "Test");

        // Act
        var count = await _attachmentDbClient.GetCommentAttachmentsCountInternalAsync(
            userId, report.Id, bug.Id, comment.Id);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact(DisplayName = "Подсчет вложений комментария - несколько вложений")]
    public async Task GetCommentAttachmentsCountAsync_MultipleAttachments_ShouldReturnCorrectCount()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, report.Id, bug.Id, "Test");

        await CreateTestCommentAttachmentAsync(userId, comment.Id);
        await CreateTestCommentAttachmentAsync(userId, comment.Id);

        // Act
        var count = await _attachmentDbClient.GetCommentAttachmentsCountInternalAsync(
            userId, report.Id, bug.Id, comment.Id);

        // Assert
        Assert.Equal(2, count);
    }

    [Fact(DisplayName = "Подсчет вложений шага бага")]
    public async Task GetBugStepAttachmentsCountAsync_MultipleAttachments_ShouldReturnCorrectCount()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step = await CreateTestBugStepAsync(userId, report.Id, bug.Id);

        await CreateTestBugStepAttachmentAsync(userId, step.Id);
        await CreateTestBugStepAttachmentAsync(userId, step.Id);

        // Act
        var count = await _attachmentDbClient.GetBugStepAttachmentsCountInternalAsync(
            report.Id, bug.Id, step.Id);

        // Assert
        Assert.Equal(2, count);
    }

    #endregion

    #region DeleteBugAttachment Tests

    [Fact(DisplayName = "Удаление вложения бага")]
    public async Task DeleteBugAttachmentAsync_ValidAttachment_ShouldDeleteAndReturnAttachment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var attachment = await CreateTestBugAttachmentAsync(userId, bug.Id);

        // Act
        var deleted = await _attachmentDbClient.DeleteBugAttachmentInternalAsync(report.Id, bug.Id, attachment.Id);

        // Assert
        Assert.NotNull(deleted);
        Assert.Equal(attachment.Id, deleted.Id);

        // Проверяем что вложение действительно удалено
        var getResult = await _attachmentDbClient.GetBugAttachmentInternalAsync(report.Id, bug.Id, attachment.Id);
        Assert.Null(getResult);
    }

    [Fact(DisplayName = "Удаление несуществующего вложения бага")]
    public async Task DeleteBugAttachmentAsync_NonExistent_ShouldReturnNull()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);

        // Act
        var result = await _attachmentDbClient.DeleteBugAttachmentInternalAsync(report.Id, bug.Id, 999999);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region DeleteCommentAttachment Tests

    [Fact(DisplayName = "Удаление вложения комментария")]
    public async Task DeleteCommentAttachmentAsync_ValidAttachment_ShouldDeleteAndReturnAttachment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, report.Id, bug.Id, "Test");
        var attachment = await CreateTestCommentAttachmentAsync(userId, comment.Id);

        // Act
        var deleted = await _attachmentDbClient.DeleteCommentAttachmentInternalAsync(
            report.Id, bug.Id, comment.Id, attachment.Id);

        // Assert
        Assert.NotNull(deleted);
        Assert.Equal(attachment.Id, deleted.Id);

        // Проверяем что вложение действительно удалено
        var getResult = await _attachmentDbClient.GetCommentAttachmentInternalAsync(
            report.Id, bug.Id, comment.Id, attachment.Id);
        Assert.Null(getResult);
    }

    [Fact(DisplayName = "Удаление несуществующего вложения комментария")]
    public async Task DeleteCommentAttachmentAsync_NonExistent_ShouldReturnNull()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, report.Id, bug.Id, "Test");

        // Act
        var result = await _attachmentDbClient.DeleteCommentAttachmentInternalAsync(
            report.Id, bug.Id, comment.Id, 999999);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Удаление вложения шага бага")]
    public async Task DeleteBugStepAttachmentAsync_ValidAttachment_ShouldDeleteAndReturnAttachment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step = await CreateTestBugStepAsync(userId, report.Id, bug.Id);
        var attachment = await CreateTestBugStepAttachmentAsync(userId, step.Id);

        // Act
        var deleted = await _attachmentDbClient.DeleteBugStepAttachmentInternalAsync(
            report.Id, bug.Id, step.Id, attachment.Id);

        // Assert
        Assert.NotNull(deleted);
        Assert.Equal(attachment.Id, deleted.Id);

        var getResult = await _attachmentDbClient.GetBugStepAttachmentInternalAsync(report.Id, bug.Id, step.Id, attachment.Id);
        Assert.Null(getResult);
    }

    #endregion

    #region DeleteCommentAttachments Tests

    [Fact(DisplayName = "Удаление всех вложений комментария")]
    public async Task DeleteCommentAttachmentsAsync_MultipleAttachments_ShouldDeleteAll()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, report.Id, bug.Id, "Test");

        var attachment1 = await CreateTestCommentAttachmentAsync(userId, comment.Id);
        var attachment2 = await CreateTestCommentAttachmentAsync(userId, comment.Id);
        var attachment3 = await CreateTestCommentAttachmentAsync(userId, comment.Id);

        // Act
        var deleted = await _attachmentDbClient.DeleteCommentAttachmentsAsync(comment.Id);

        // Assert
        Assert.NotNull(deleted);
        Assert.Equal(3, deleted.Length);
        Assert.Contains(deleted, a => a.Id == attachment1.Id);
        Assert.Contains(deleted, a => a.Id == attachment2.Id);
        Assert.Contains(deleted, a => a.Id == attachment3.Id);
    }

    [Fact(DisplayName = "Удаление вложений комментария без вложений")]
    public async Task DeleteCommentAttachmentsAsync_NoAttachments_ShouldReturnEmpty()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, report.Id, bug.Id, "Test");

        // Act
        var deleted = await _attachmentDbClient.DeleteCommentAttachmentsAsync(comment.Id);

        // Assert
        Assert.NotNull(deleted);
        Assert.Empty(deleted);
    }

    [Fact(DisplayName = "Удаление всех вложений шага бага")]
    public async Task DeleteBugStepAttachmentsAsync_MultipleAttachments_ShouldDeleteAll()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step = await CreateTestBugStepAsync(userId, report.Id, bug.Id);

        var attachment1 = await CreateTestBugStepAttachmentAsync(userId, step.Id);
        var attachment2 = await CreateTestBugStepAttachmentAsync(userId, step.Id);

        // Act
        var deleted = await _attachmentDbClient.DeleteBugStepAttachmentsAsync(step.Id);

        // Assert
        Assert.NotNull(deleted);
        Assert.Equal(2, deleted.Length);
        Assert.Contains(deleted, a => a.Id == attachment1.Id);
        Assert.Contains(deleted, a => a.Id == attachment2.Id);
    }

    #endregion

    #region Complex Workflow Tests

    [Fact(DisplayName = "Полный жизненный цикл вложения: создание -> обновление -> получение -> удаление")]
    public async Task AttachmentLifecycle_CreateUpdateGetDelete_ShouldWorkCorrectly()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);

        // Act & Assert - Создание
        var created = await CreateTestBugAttachmentAsync(userId, bug.Id);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);

        // Act & Assert - Обновление
        var updateModel = new AttachmentUpdate
        {
            Id = created.Id,
            StorageKey = "updated_key.jpg",
            StorageKind = 2,
            LengthBytes = 5000,
            FileName = "updated.jpg",
            MimeType = "image/jpeg",
            HasPreview = true,
            IsGzipCompressed = false
        };
        var updated = await _attachmentDbClient.UpdateAttachmentAsync(updateModel);
        Assert.NotNull(updated);
        Assert.Equal("updated.jpg", updated.FileName);

        // Act & Assert - Получение
        var retrieved = await _attachmentDbClient.GetBugAttachmentInternalAsync(report.Id, bug.Id, created.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Id);

        // Act & Assert - Удаление
        var deleted = await _attachmentDbClient.DeleteBugAttachmentInternalAsync(report.Id, bug.Id, created.Id);
        Assert.NotNull(deleted);
        var afterDelete = await _attachmentDbClient.GetBugAttachmentInternalAsync(report.Id, bug.Id, created.Id);
        Assert.Null(afterDelete);
    }

    #endregion

    #region Helper Methods

    private async Task<Bugget.Domain.Reports.ReportSummary> CreateTestReportAsync(
        string userId,
        string? organizationId = null)
    {
        var reportDto = new ReportCreateDto
        {
            Title = $"Test Report {Guid.NewGuid()}"
        };
        return await _reportsDbClient.CreateReportAsync(userId, null, organizationId, reportDto);
    }

    private async Task<Bugget.Domain.Bugs.BugSummary> CreateTestBugAsync(
        string userId,
        int reportId,
        string? organizationId = null)
    {
        var bugDto = new BugDto
        {
            Receive = "Test bug receive",
            Expect = "Test bug expect"
        };
        return await _bugsDbClient.CreateBugAsync(userId, reportId, bugDto);
    }

    private async Task<Bugget.Domain.Comments.CommentSummary> CreateTestCommentAsync(
        string userId,
        int reportId,
        int bugId,
        string text,
        string? organizationId = null)
    {
        return await _commentsDbClient.CreateCommentAsync(userId, bugId, text);
    }

    private async Task<Attachment> CreateTestBugAttachmentAsync(string userId, int bugId)
    {
        var createModel = new AttachmentCreate
        {
            EntityId = bugId,
            AttachType = AttachType_BugFact,
            StorageKey = $"test/bug_{bugId}_{Guid.NewGuid()}.jpg",
            StorageKind = 1,
            CreatorUserId = userId,
            LengthBytes = 1024,
            FileName = "test_file.jpg",
            MimeType = "image/jpeg"
        };
        return await _attachmentDbClient.CreateAttachment(createModel);
    }

    private async Task<Attachment> CreateTestCommentAttachmentAsync(string userId, int commentId)
    {
        var createModel = new AttachmentCreate
        {
            EntityId = commentId,
            AttachType = AttachType_Comment,
            StorageKey = $"test/comment_{commentId}_{Guid.NewGuid()}.jpg",
            StorageKind = 1,
            CreatorUserId = userId,
            LengthBytes = 2048,
            FileName = "test_comment_file.jpg",
            MimeType = "image/jpeg"
        };
        return await _attachmentDbClient.CreateAttachment(createModel);
    }

    private async Task<BugStepSummary> CreateTestBugStepAsync(
        string userId,
        int reportId,
        int bugId)
    {
        var bugStepDto = new BugStepDto
        {
            Text = "Test bug step"
        };

        return await _bugStepsDbClient.CreateBugStepAsync(userId, bugId, bugStepDto);
    }

    private async Task<Attachment> CreateTestBugStepAttachmentAsync(string userId, int stepId)
    {
        var createModel = new AttachmentCreate
        {
            EntityId = stepId,
            AttachType = AttachType_BugStep,
            StorageKey = $"test/bug_step_{stepId}_{Guid.NewGuid()}.jpg",
            StorageKind = 1,
            CreatorUserId = userId,
            LengthBytes = 2048,
            FileName = "test_bug_step_file.jpg",
            MimeType = "image/jpeg"
        };

        return await _attachmentDbClient.CreateAttachment(createModel);
    }

    #endregion
}

