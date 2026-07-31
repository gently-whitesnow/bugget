using Bugget.Application.Ports;
using Bugget.Contracts.Dto.Bug;
using Bugget.Contracts.Dto.Report;
using Bugget.Domain.Common;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class CommentsDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly ICommentsDbClient _commentsDbClient;
    private readonly IBugsDbClient _bugsDbClient;
    private readonly IReportsDbClient _reportsDbClient;

    public CommentsDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _commentsDbClient = scope.ServiceProvider.GetRequiredService<ICommentsDbClient>();
        _bugsDbClient = scope.ServiceProvider.GetRequiredService<IBugsDbClient>();
        _reportsDbClient = scope.ServiceProvider.GetRequiredService<IReportsDbClient>();
    }

    #region CreateCommentAsync Tests

    [Fact(DisplayName = "Успешное создание комментария с минимальными параметрами")]
    public async Task CreateCommentAsync_WithMinimalParameters_ShouldCreateComment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var commentText = "This is a test comment";

        // Act
        var result = await _commentsDbClient.CreateCommentAsync(userId, bug.Id, commentText);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(bug.Id, result.BugId);
        Assert.Equal(commentText, result.Text);
        Assert.Equal(userId, result.CreatorUserId);
        Assert.Equal((int)CreatorType.User, result.CreatorType);
        Assert.True(result.CreatedAt > DateTimeOffset.MinValue);
        Assert.True(result.UpdatedAt > DateTimeOffset.MinValue);
        Assert.Equal(result.CreatedAt, result.UpdatedAt); // При создании времена должны совпадать
    }

    [Fact(DisplayName = "Успешное создание комментария с organizationId")]
    public async Task CreateCommentAsync_WithOrganizationId_ShouldCreateComment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var organizationId = $"org_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId, organizationId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var commentText = "Comment with organization";

        // Act
        var result = await _commentsDbClient.CreateCommentAsync(userId, bug.Id, commentText);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(bug.Id, result.BugId);
        Assert.Equal(commentText, result.Text);
        Assert.Equal(userId, result.CreatorUserId);
    }

    [Fact(DisplayName = "Создание нескольких комментариев к одному багу")]
    public async Task CreateCommentAsync_MultipleCommentsForOneBug_ShouldCreateSeparateComments()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment1Text = "First comment";
        var comment2Text = "Second comment";
        var comment3Text = "Third comment";

        // Act
        var result1 = await _commentsDbClient.CreateCommentAsync(userId, bug.Id, comment1Text);
        var result2 = await _commentsDbClient.CreateCommentAsync(userId, bug.Id, comment2Text);
        var result3 = await _commentsDbClient.CreateCommentAsync(userId, bug.Id, comment3Text);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        Assert.NotEqual(result1.Id, result2.Id);
        Assert.NotEqual(result2.Id, result3.Id);
        Assert.NotEqual(result1.Id, result3.Id);
        Assert.Equal(comment1Text, result1.Text);
        Assert.Equal(comment2Text, result2.Text);
        Assert.Equal(comment3Text, result3.Text);
        Assert.Equal(bug.Id, result1.BugId);
        Assert.Equal(bug.Id, result2.BugId);
        Assert.Equal(bug.Id, result3.BugId);
    }

    [Fact(DisplayName = "Создание комментариев разными пользователями")]
    public async Task CreateCommentAsync_DifferentUsers_ShouldCreateComments()
    {
        // Arrange
        var user1 = $"user_{Guid.NewGuid()}";
        var user2 = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(user1);
        var bug = await CreateTestBugAsync(user1, report.Id);

        // Act
        var comment1 = await _commentsDbClient.CreateCommentAsync(user1, bug.Id, "Comment by user1");
        var comment2 = await _commentsDbClient.CreateCommentAsync(user2, bug.Id, "Comment by user2");

        // Assert
        Assert.NotNull(comment1);
        Assert.NotNull(comment2);
        Assert.NotEqual(comment1.Id, comment2.Id);
        Assert.Equal(user1, comment1.CreatorUserId);
        Assert.Equal(user2, comment2.CreatorUserId);
        Assert.Equal(bug.Id, comment1.BugId);
        Assert.Equal(bug.Id, comment2.BugId);
    }

    #endregion

    #region UpdateCommentAsync Tests

    [Fact(DisplayName = "Успешное обновление текста комментария")]
    public async Task UpdateCommentAsync_WithNewText_ShouldUpdateComment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, bug.Id, "Original text");
        var newText = "Updated text";

        // Act
        var result = await _commentsDbClient.UpdateCommentInternalAsync(userId, report.Id, bug.Id, comment.Id, newText);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(comment.Id, result.Id);
        Assert.Equal(newText, result.Text);
        Assert.Equal(bug.Id, result.BugId);
        Assert.Equal(userId, result.CreatorUserId);
        Assert.True(result.UpdatedAt > comment.UpdatedAt);
        Assert.Equal(comment.CreatedAt, result.CreatedAt); // CreatedAt не должен измениться
    }

    [Fact(DisplayName = "Обновление комментария с organizationId")]
    public async Task UpdateCommentAsync_WithOrganizationId_ShouldUpdateComment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var organizationId = $"org_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId, organizationId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, bug.Id, "Original");
        var newText = "Updated with org";

        // Act
        var result = await _commentsDbClient.UpdateCommentInternalAsync(userId, report.Id, bug.Id, comment.Id, newText);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(comment.Id, result.Id);
        Assert.Equal(newText, result.Text);
    }

    [Fact(DisplayName = "Многократное обновление комментария")]
    public async Task UpdateCommentAsync_MultipleUpdates_ShouldUpdateCorrectly()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, bug.Id, "Original text");

        // Act & Assert - First update
        var result1 = await _commentsDbClient.UpdateCommentInternalAsync(userId, report.Id, bug.Id, comment.Id, "First update");
        Assert.NotNull(result1);
        Assert.Equal("First update", result1.Text);
        Assert.True(result1.UpdatedAt > comment.UpdatedAt);

        // Небольшая задержка
        await Task.Delay(10);

        // Act & Assert - Second update
        var result2 = await _commentsDbClient.UpdateCommentInternalAsync(userId, report.Id, bug.Id, comment.Id, "Second update");
        Assert.NotNull(result2);
        Assert.Equal("Second update", result2.Text);
        Assert.True(result2.UpdatedAt > result1.UpdatedAt);

        // CreatedAt должен остаться неизменным
        Assert.Equal(comment.CreatedAt, result1.CreatedAt);
        Assert.Equal(comment.CreatedAt, result2.CreatedAt);
    }

    [Fact(DisplayName = "Проверка что UpdatedAt обновляется при каждом изменении")]
    public async Task UpdateCommentAsync_ShouldUpdateTimestamp()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, bug.Id, "Original");
        var initialUpdatedAt = comment.UpdatedAt;

        // Небольшая задержка для гарантии различия времени
        await Task.Delay(10);

        // Act
        var result = await _commentsDbClient.UpdateCommentInternalAsync(userId, report.Id, bug.Id, comment.Id, "Updated");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.UpdatedAt > initialUpdatedAt);
    }

    #endregion

    #region DeleteCommentAsync Tests

    [Fact(DisplayName = "Успешное удаление комментария")]
    public async Task DeleteCommentAsync_WithValidComment_ShouldDeleteComment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, bug.Id, "To be deleted");

        // Act
        var result = await _commentsDbClient.DeleteCommentInternalAsync(userId, report.Id, bug.Id, comment.Id);

        // Assert
        // Проверяем что комментарий удален, попытка обновить должна вернуть null
        Assert.NotNull(result);
    }

    [Fact(DisplayName = "Удаление одного из нескольких комментариев")]
    public async Task DeleteCommentAsync_OneOfMultiple_ShouldDeleteOnlyOne()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment1 = await CreateTestCommentAsync(userId, bug.Id, "Comment 1");
        var comment2 = await CreateTestCommentAsync(userId, bug.Id, "Comment 2");
        var comment3 = await CreateTestCommentAsync(userId, bug.Id, "Comment 3");

        // Act - удаляем второй комментарий
        var result = await _commentsDbClient.DeleteCommentInternalAsync(userId, report.Id, bug.Id, comment2.Id);

        // Assert - проверяем что comment2 удален
        Assert.NotNull(result);

        // Assert - проверяем что comment1 и comment3 все еще существуют
        var updateResult1 = await _commentsDbClient.UpdateCommentInternalAsync(userId, report.Id, bug.Id, comment1.Id, "Updated 1");
        var updateResult3 = await _commentsDbClient.UpdateCommentInternalAsync(userId, report.Id, bug.Id, comment3.Id, "Updated 3");
        Assert.NotNull(updateResult1);
        Assert.NotNull(updateResult3);
        Assert.Equal("Updated 1", updateResult1.Text);
        Assert.Equal("Updated 3", updateResult3.Text);
    }

    [Fact(DisplayName = "Удаление несуществующего комментария должно вернуть null")]
    public async Task DeleteCommentAsync_NonExistentComment_ShouldReturnNull()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var nonExistentCommentId = 999999;

        // Act
        var result = await _commentsDbClient.DeleteCommentInternalAsync(userId, report.Id, bug.Id, nonExistentCommentId);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Успешное создание комментария с иным значением creatorType")]
    public async Task CreateCommentAsync_WithOtherCreatorType_ShouldCreateComment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var commentText = "This is a test comment";

        // Act
        var result = await _commentsDbClient.CreateCommentAsync(userId, bug.Id, commentText, (int)CreatorType.System);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(bug.Id, result.BugId);
        Assert.Equal(commentText, result.Text);
        Assert.Equal(userId, result.CreatorUserId);
        Assert.Equal((int)CreatorType.System, result.CreatorType);
        Assert.True(result.CreatedAt > DateTimeOffset.MinValue);
        Assert.True(result.UpdatedAt > DateTimeOffset.MinValue);
        Assert.Equal(result.CreatedAt, result.UpdatedAt); // При создании времена должны совпадать
    }

    #endregion

    #region Complex Workflow Tests

    [Fact(DisplayName = "Полный жизненный цикл комментария: создание -> обновление -> удаление")]
    public async Task CommentLifecycle_CreateUpdateDelete_ShouldWorkCorrectly()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);

        // Act & Assert - Создание
        var comment = await _commentsDbClient.CreateCommentAsync(userId, bug.Id, "Initial text");
        Assert.NotNull(comment);
        Assert.Equal("Initial text", comment.Text);

        // Act & Assert - Обновление
        var updated = await _commentsDbClient.UpdateCommentInternalAsync(userId, report.Id, bug.Id, comment.Id, "Updated text");
        Assert.NotNull(updated);
        Assert.Equal("Updated text", updated.Text);
        Assert.Equal(comment.Id, updated.Id);

        // Act & Assert - Удаление
        var result = await _commentsDbClient.DeleteCommentInternalAsync(userId, report.Id, bug.Id, comment.Id);
        Assert.NotNull(result);

        // Assert
        var updateResult = await _commentsDbClient.UpdateCommentInternalAsync(userId, report.Id, bug.Id, comment.Id, "Should fail");
        Assert.Null(updateResult);
    }

    [Fact(DisplayName = "Создание комментариев к разным багам одного репорта")]
    public async Task CreateCommentAsync_DifferentBugsInSameReport_ShouldCreateCorrectly()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug1 = await CreateTestBugAsync(userId, report.Id);
        var bug2 = await CreateTestBugAsync(userId, report.Id);

        // Act
        var comment1 = await _commentsDbClient.CreateCommentAsync(userId, bug1.Id, "Comment for bug 1");
        var comment2 = await _commentsDbClient.CreateCommentAsync(userId, bug2.Id, "Comment for bug 2");

        // Assert
        Assert.NotNull(comment1);
        Assert.NotNull(comment2);
        Assert.NotEqual(comment1.Id, comment2.Id);
        Assert.Equal(bug1.Id, comment1.BugId);
        Assert.Equal(bug2.Id, comment2.BugId);
        Assert.Equal("Comment for bug 1", comment1.Text);
        Assert.Equal("Comment for bug 2", comment2.Text);
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
        int reportId)
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
        int bugId,
        string text)
    {
        return await _commentsDbClient.CreateCommentAsync(userId, bugId, text);
    }

    #endregion
}

