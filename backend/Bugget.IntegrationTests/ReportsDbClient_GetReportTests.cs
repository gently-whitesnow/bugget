using Bugget.DA.Interfaces;
using Bugget.Entities.BO.Common;
using Bugget.Entities.DbModels.Attachment;
using Bugget.Entities.DbModels.BugSteps;
using Bugget.Entities.DTO.Bug;
using Bugget.Entities.DTO.BugStep;
using Bugget.Entities.DTO.Report;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class ReportsDbClient_GetReportTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IReportsDbClient _reportsDbClient;
    private readonly IBugsDbClient _bugsDbClient;
    private readonly ICommentsDbClient _commentsDbClient;
    private readonly IParticipantsDbClient _participantsDbClient;
    private readonly IAttachmentDbClient _attachmentDbClient;
    private readonly IBugStepsDbClient _bugStepsDbClient;

    // AttachType константы
    private const int AttachType_BugFact = 0;
    private const int AttachType_BugExpected = 1;
    private const int AttachType_Comment = 2;
    private const int AttachType_BugStep = 3;

    public ReportsDbClient_GetReportTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _reportsDbClient = scope.ServiceProvider.GetRequiredService<IReportsDbClient>();
        _bugsDbClient = scope.ServiceProvider.GetRequiredService<IBugsDbClient>();
        _commentsDbClient = scope.ServiceProvider.GetRequiredService<ICommentsDbClient>();
        _participantsDbClient = scope.ServiceProvider.GetRequiredService<IParticipantsDbClient>();
        _attachmentDbClient = scope.ServiceProvider.GetRequiredService<IAttachmentDbClient>();
        _bugStepsDbClient = scope.ServiceProvider.GetRequiredService<IBugStepsDbClient>();
    }

    [Fact(DisplayName = "Получение простого репорта без багов")]
    public async Task GetReportAsync_EmptyReport_ShouldReturnReport()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var created = await CreateTestReportAsync(userId);

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(created.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal(created.Title, result.Title);
        Assert.Equal(userId, result.CreatorUserId);
        Assert.Equal(userId, result.ResponsibleUserId);
        Assert.NotNull(result.Bugs);
        Assert.Empty(result.Bugs);
        Assert.NotNull(result.ParticipantsUserIds);
    }

    [Fact(DisplayName = "Получение репорта с одним багом")]
    public async Task GetReportAsync_WithOneBug_ShouldReturnReportWithBug()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(report.Id, result.Id);
        Assert.NotNull(result.Bugs);
        Assert.Single(result.Bugs);
        Assert.Equal(bug.Id, result.Bugs[0].Id);
        Assert.Equal(bug.Receive, result.Bugs[0].Receive);
        Assert.Equal(bug.Expect, result.Bugs[0].Expect);
        Assert.NotNull(result.Bugs[0].Comments);
        Assert.Empty(result.Bugs[0].Comments!);
        Assert.NotNull(result.Bugs[0].Steps);
        Assert.Empty(result.Bugs[0].Steps!);
    }

    [Fact(DisplayName = "Получение репорта с несколькими багами")]
    public async Task GetReportAsync_WithMultipleBugs_ShouldReturnAllBugs()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug1 = await CreateTestBugAsync(userId, report.Id, "Bug 1 receive", "Bug 1 expect");
        var bug2 = await CreateTestBugAsync(userId, report.Id, "Bug 2 receive", "Bug 2 expect");
        var bug3 = await CreateTestBugAsync(userId, report.Id, "Bug 3 receive", "Bug 3 expect");

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Bugs);
        Assert.Equal(3, result.Bugs.Length);
        Assert.Contains(result.Bugs, b => b.Id == bug1.Id);
        Assert.Contains(result.Bugs, b => b.Id == bug2.Id);
        Assert.Contains(result.Bugs, b => b.Id == bug3.Id);
    }

    [Fact(DisplayName = "Получение репорта с багом и комментариями")]
    public async Task GetReportAsync_WithBugAndComments_ShouldReturnCommentsInBug()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment1 = await CreateTestCommentAsync(userId, bug.Id, "Comment 1");
        var comment2 = await CreateTestCommentAsync(userId, bug.Id, "Comment 2", (int)CreatorType.System);

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Bugs);
        Assert.Single(result.Bugs);
        var bugResult = result.Bugs[0];
        Assert.NotNull(bugResult.Comments);
        Assert.Equal(2, bugResult.Comments.Length);
        Assert.Contains(bugResult.Comments, c => c.Id == comment1.Id && c.Text == "Comment 1");
        Assert.Contains(bugResult.Comments, c => c.Id == comment2.Id && c.Text == "Comment 2" && c.CreatorType == (int)CreatorType.System);
    }

    [Fact(DisplayName = "Получение репорта с багом и вложениями бага")]
    public async Task GetReportAsync_WithBugAttachments_ShouldReturnAttachmentsInBug()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var attachment1 = await CreateTestBugAttachmentAsync(userId, bug.Id, "file1.jpg");
        var attachment2 = await CreateTestBugAttachmentAsync(userId, bug.Id, "file2.png");

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Bugs);
        Assert.Single(result.Bugs);
        var bugResult = result.Bugs[0];
        Assert.NotNull(bugResult.Attachments);
        Assert.Equal(2, bugResult.Attachments.Length);
        Assert.Contains(bugResult.Attachments, a => a.Id == attachment1.Id && a.FileName == "file1.jpg");
        Assert.Contains(bugResult.Attachments, a => a.Id == attachment2.Id && a.FileName == "file2.png");
    }

    [Fact(DisplayName = "Получение репорта с комментарием и вложениями комментария")]
    public async Task GetReportAsync_WithCommentAttachments_ShouldReturnAttachmentsInComment()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, bug.Id, "Test comment");
        var attachment = await CreateTestCommentAttachmentAsync(userId, comment.Id, "comment_file.pdf");

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Bugs);
        Assert.Single(result.Bugs);
        var bugResult = result.Bugs[0];
        Assert.NotNull(bugResult.Comments);
        Assert.Single(bugResult.Comments);
        var commentResult = bugResult.Comments[0];
        Assert.NotNull(commentResult.Attachments);
        Assert.Single(commentResult.Attachments);
        Assert.Equal(attachment.Id, commentResult.Attachments[0].Id);
        Assert.Equal("comment_file.pdf", commentResult.Attachments[0].FileName);
    }

    [Fact(DisplayName = "Получение репорта с участниками")]
    public async Task GetReportAsync_WithParticipants_ShouldReturnParticipants()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var participant1 = $"user_{Guid.NewGuid()}";
        var participant2 = $"user_{Guid.NewGuid()}";

        await _participantsDbClient.AddParticipantIfNotExistAsync(report.Id, participant1);
        await _participantsDbClient.AddParticipantIfNotExistAsync(report.Id, participant2);

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ParticipantsUserIds);
        Assert.Contains(participant1, result.ParticipantsUserIds);
        Assert.Contains(participant2, result.ParticipantsUserIds);
    }

    [Fact(DisplayName = "Получение полного репорта со всеми связанными данными")]
    public async Task GetReportAsync_CompleteReport_ShouldReturnFullGraph()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);

        // Добавляем участников
        var participant1 = $"user_{Guid.NewGuid()}";
        await _participantsDbClient.AddParticipantIfNotExistAsync(report.Id, participant1);

        // Создаем баг с комментариями и вложениями
        var bug = await CreateTestBugAsync(userId, report.Id);
        var bugAttachment = await CreateTestBugAttachmentAsync(userId, bug.Id, "bug_file.jpg");

        var comment = await CreateTestCommentAsync(userId, bug.Id, "Test comment");
        var commentAttachment = await CreateTestCommentAttachmentAsync(userId, comment.Id, "comment_file.pdf");

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(report.Id, result.Id);

        // Проверяем участников
        Assert.Contains(participant1, result.ParticipantsUserIds);

        // Проверяем баги
        Assert.NotNull(result.Bugs);
        Assert.Single(result.Bugs);
        var bugResult = result.Bugs[0];
        Assert.Equal(bug.Id, bugResult.Id);

        // Проверяем вложения бага
        Assert.NotNull(bugResult.Attachments);
        Assert.Single(bugResult.Attachments);
        Assert.Equal(bugAttachment.Id, bugResult.Attachments[0].Id);

        // Проверяем комментарии
        Assert.NotNull(bugResult.Comments);
        Assert.Single(bugResult.Comments);
        var commentResult = bugResult.Comments[0];
        Assert.Equal(comment.Id, commentResult.Id);

        // Проверяем вложения комментария
        Assert.NotNull(commentResult.Attachments);
        Assert.Single(commentResult.Attachments);
        Assert.Equal(commentAttachment.Id, commentResult.Attachments[0].Id);
    }

    [Fact(DisplayName = "Получение несуществующего репорта возвращает null")]
    public async Task GetReportAsync_NonExistentReport_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = 999999;

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Комментарии группируются по багам корректно")]
    public async Task GetReportAsync_MultipleБugsWithComments_ShouldGroupCommentsCorrectly()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);

        var bug1 = await CreateTestBugAsync(userId, report.Id, "Bug 1", "Fix 1");
        var bug2 = await CreateTestBugAsync(userId, report.Id, "Bug 2", "Fix 2");

        var comment1ForBug1 = await CreateTestCommentAsync(userId, bug1.Id, "Bug1 Comment1");
        var comment2ForBug1 = await CreateTestCommentAsync(userId, bug1.Id, "Bug1 Comment2");
        var comment1ForBug2 = await CreateTestCommentAsync(userId, bug2.Id, "Bug2 Comment1");

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Bugs);
        Assert.Equal(2, result.Bugs.Length);

        var bug1Result = result.Bugs.First(b => b.Id == bug1.Id);
        var bug2Result = result.Bugs.First(b => b.Id == bug2.Id);

        Assert.NotNull(bug1Result.Comments);
        Assert.Equal(2, bug1Result.Comments.Length);
        Assert.NotNull(bug2Result.Comments);
        Assert.Single(bug2Result.Comments);

        Assert.Contains(bug1Result.Comments, c => c.Text == "Bug1 Comment1");
        Assert.Contains(bug1Result.Comments, c => c.Text == "Bug1 Comment2");
        Assert.Equal("Bug2 Comment1", bug2Result.Comments[0].Text);
    }

    [Fact(DisplayName = "Вложения группируются по типам корректно")]
    public async Task GetReportAsync_WithDifferentAttachmentTypes_ShouldGroupCorrectly()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var comment = await CreateTestCommentAsync(userId, bug.Id, "Test");

        // Вложения к багу (AttachType 0 и 1)
        var bugAttachment = await CreateTestBugAttachmentAsync(userId, bug.Id, "bug.jpg");

        // Вложение к комментарию (AttachType 2)
        var commentAttachment = await CreateTestCommentAttachmentAsync(userId, comment.Id, "comment.pdf");

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Bugs);
        var bugResult = result.Bugs[0];
        Assert.NotNull(bugResult.Comments);
        var commentResult = bugResult.Comments[0];

        // Вложения бага не должны содержать вложения комментария
        Assert.NotNull(bugResult.Attachments);
        Assert.Single(bugResult.Attachments);
        Assert.Equal(bugAttachment.Id, bugResult.Attachments[0].Id);
        Assert.All(bugResult.Attachments, a => Assert.NotEqual(AttachType_Comment, a.AttachType));

        // Вложения комментария не должны содержать вложения бага
        Assert.NotNull(commentResult.Attachments);
        Assert.Single(commentResult.Attachments);
        Assert.Equal(commentAttachment.Id, commentResult.Attachments[0].Id);
        Assert.All(commentResult.Attachments, a => Assert.Equal(AttachType_Comment, a.AttachType));
    }

    [Fact(DisplayName = "Комментарии сортируются по времени создания")]
    public async Task GetReportAsync_Comments_ShouldBeSortedByCreatedAt()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);

        // Создаем комментарии с небольшими задержками
        var comment1 = await CreateTestCommentAsync(userId, bug.Id, "First");
        await Task.Delay(10);
        var comment2 = await CreateTestCommentAsync(userId, bug.Id, "Second");
        await Task.Delay(10);
        var comment3 = await CreateTestCommentAsync(userId, bug.Id, "Third");

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Bugs);
        var comments = result.Bugs[0].Comments;
        Assert.NotNull(comments);
        Assert.Equal(3, comments.Length);

        // Проверяем что комментарии отсортированы по CreatedAt
        Assert.True(comments[0].CreatedAt <= comments[1].CreatedAt);
        Assert.True(comments[1].CreatedAt <= comments[2].CreatedAt);

        Assert.Equal("First", comments[0].Text);
        Assert.Equal("Second", comments[1].Text);
        Assert.Equal("Third", comments[2].Text);
    }

    [Fact(DisplayName = "Получение репорта с багом и шагами воспроизведения")]
    public async Task GetReportAsync_WithBugSteps_ShouldReturnStepsInBug()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step1 = await CreateTestBugStepAsync(userId, bug.Id, "Step 1: Open application");
        var step2 = await CreateTestBugStepAsync(userId, bug.Id, "Step 2: Click button");
        var step3 = await CreateTestBugStepAsync(userId, bug.Id, "Step 3: Verify result");

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Bugs);
        Assert.Single(result.Bugs);
        var bugResult = result.Bugs[0];
        Assert.NotNull(bugResult.Steps);
        Assert.Equal(3, bugResult.Steps.Length);
        Assert.Contains(bugResult.Steps, s => s.Id == step1.Id && s.Text == "Step 1: Open application");
        Assert.Contains(bugResult.Steps, s => s.Id == step2.Id && s.Text == "Step 2: Click button");
        Assert.Contains(bugResult.Steps, s => s.Id == step3.Id && s.Text == "Step 3: Verify result");
    }

    [Fact(DisplayName = "Шаги воспроизведения группируются по багам корректно")]
    public async Task GetReportAsync_MultipleBugsWithSteps_ShouldGroupStepsCorrectly()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);

        var bug1 = await CreateTestBugAsync(userId, report.Id, "Bug 1", "Fix 1");
        var bug2 = await CreateTestBugAsync(userId, report.Id, "Bug 2", "Fix 2");

        var step1ForBug1 = await CreateTestBugStepAsync(userId, bug1.Id, "Bug1 Step1");
        var step2ForBug1 = await CreateTestBugStepAsync(userId, bug1.Id, "Bug1 Step2");
        var step1ForBug2 = await CreateTestBugStepAsync(userId, bug2.Id, "Bug2 Step1");

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Bugs);
        Assert.Equal(2, result.Bugs.Length);

        var bug1Result = result.Bugs.First(b => b.Id == bug1.Id);
        var bug2Result = result.Bugs.First(b => b.Id == bug2.Id);

        Assert.NotNull(bug1Result.Steps);
        Assert.Equal(2, bug1Result.Steps.Length);
        Assert.NotNull(bug2Result.Steps);
        Assert.Single(bug2Result.Steps);

        Assert.Contains(bug1Result.Steps, s => s.Text == "Bug1 Step1");
        Assert.Contains(bug1Result.Steps, s => s.Text == "Bug1 Step2");
        Assert.Equal("Bug2 Step1", bug2Result.Steps[0].Text);
    }

    [Fact(DisplayName = "Шаги воспроизведения сортируются по StepNumber")]
    public async Task GetReportAsync_Steps_ShouldBeSortedByStepNumber()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);

        // Создаем шаги - они автоматически получают step_number по порядку создания
        var step1 = await CreateTestBugStepAsync(userId, bug.Id, "First step");
        var step2 = await CreateTestBugStepAsync(userId, bug.Id, "Second step");
        var step3 = await CreateTestBugStepAsync(userId, bug.Id, "Third step");

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Bugs);
        var steps = result.Bugs[0].Steps;
        Assert.NotNull(steps);
        Assert.Equal(3, steps.Length);

        // Проверяем что шаги отсортированы по StepNumber
        Assert.True(steps[0].StepNumber <= steps[1].StepNumber);
        Assert.True(steps[1].StepNumber <= steps[2].StepNumber);

        Assert.Equal("First step", steps[0].Text);
        Assert.Equal("Second step", steps[1].Text);
        Assert.Equal("Third step", steps[2].Text);

        // Проверяем что StepNumber соответствует порядку
        Assert.Equal(1, steps[0].StepNumber);
        Assert.Equal(2, steps[1].StepNumber);
        Assert.Equal(3, steps[2].StepNumber);
    }

    [Fact(DisplayName = "Получение репорта с шагами бага и вложениями шагов")]
    public async Task GetReportAsync_WithBugStepAttachments_ShouldReturnAttachmentsInStep()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step = await CreateTestBugStepAsync(userId, bug.Id, "Step with attachment");
        var stepAttachment1 = await CreateTestBugStepAttachmentAsync(userId, step.Id, "step_file1.jpg");
        var stepAttachment2 = await CreateTestBugStepAttachmentAsync(userId, step.Id, "step_file2.png");

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Bugs);
        Assert.Single(result.Bugs);
        var bugResult = result.Bugs[0];
        Assert.NotNull(bugResult.Steps);
        Assert.Single(bugResult.Steps);
        var stepResult = bugResult.Steps[0];
        Assert.NotNull(stepResult.Attachments);
        Assert.Equal(2, stepResult.Attachments.Length);
        Assert.Contains(stepResult.Attachments, a => a.Id == stepAttachment1.Id && a.FileName == "step_file1.jpg");
        Assert.Contains(stepResult.Attachments, a => a.Id == stepAttachment2.Id && a.FileName == "step_file2.png");
    }

    [Fact(DisplayName = "Вложения шагов группируются корректно и не попадают в вложения бага")]
    public async Task GetReportAsync_WithBugAndStepAttachments_ShouldGroupCorrectly()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step = await CreateTestBugStepAsync(userId, bug.Id, "Step with attachment");

        // Вложение к багу
        var bugAttachment = await CreateTestBugAttachmentAsync(userId, bug.Id, "bug.jpg");

        // Вложение к шагу
        var stepAttachment = await CreateTestBugStepAttachmentAsync(userId, step.Id, "step.pdf");

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Bugs);
        var bugResult = result.Bugs[0];
        Assert.NotNull(bugResult.Steps);
        var stepResult = bugResult.Steps[0];

        // Вложения бага не должны содержать вложения шага
        Assert.NotNull(bugResult.Attachments);
        Assert.Single(bugResult.Attachments);
        Assert.Equal(bugAttachment.Id, bugResult.Attachments[0].Id);
        Assert.All(bugResult.Attachments, a => Assert.NotEqual(AttachType_BugStep, a.AttachType));

        // Вложения шага не должны содержать вложения бага
        Assert.NotNull(stepResult.Attachments);
        Assert.Single(stepResult.Attachments);
        Assert.Equal(stepAttachment.Id, stepResult.Attachments[0].Id);
        Assert.All(stepResult.Attachments, a => Assert.Equal(AttachType_BugStep, a.AttachType));
    }

    [Fact(DisplayName = "Вложения шагов группируются по шагам корректно")]
    public async Task GetReportAsync_MultipleStepsWithAttachments_ShouldGroupAttachmentsCorrectly()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);

        var step1 = await CreateTestBugStepAsync(userId, bug.Id, "Step 1");
        var step1Attachment1 = await CreateTestBugStepAttachmentAsync(userId, step1.Id, "step1_file1.jpg");
        var step1Attachment2 = await CreateTestBugStepAttachmentAsync(userId, step1.Id, "step1_file2.png");

        var step2 = await CreateTestBugStepAsync(userId, bug.Id, "Step 2");
        var step2Attachment = await CreateTestBugStepAttachmentAsync(userId, step2.Id, "step2_file.pdf");

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Bugs);
        var bugResult = result.Bugs[0];
        Assert.NotNull(bugResult.Steps);
        Assert.Equal(2, bugResult.Steps.Length);

        var step1Result = bugResult.Steps.First(s => s.Id == step1.Id);
        var step2Result = bugResult.Steps.First(s => s.Id == step2.Id);

        Assert.NotNull(step1Result.Attachments);
        Assert.Equal(2, step1Result.Attachments.Length);
        Assert.Contains(step1Result.Attachments, a => a.Id == step1Attachment1.Id);
        Assert.Contains(step1Result.Attachments, a => a.Id == step1Attachment2.Id);

        Assert.NotNull(step2Result.Attachments);
        Assert.Single(step2Result.Attachments);
        Assert.Equal(step2Attachment.Id, step2Result.Attachments[0].Id);
    }

    [Fact(DisplayName = "Получение полного репорта со всеми данными, включая шаги воспроизведения")]
    public async Task GetReportAsync_CompleteReportWithSteps_ShouldReturnFullGraph()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);

        // Добавляем участников
        var participant1 = $"user_{Guid.NewGuid()}";
        await _participantsDbClient.AddParticipantIfNotExistAsync(report.Id, participant1);

        // Создаем баг с комментариями, вложениями и шагами
        var bug = await CreateTestBugAsync(userId, report.Id);
        var bugAttachment = await CreateTestBugAttachmentAsync(userId, bug.Id, "bug_file.jpg");
        var bugStep = await CreateTestBugStepAsync(userId, bug.Id, "Step to reproduce");
        var stepAttachment = await CreateTestBugStepAttachmentAsync(userId, bugStep.Id, "step_file.png");

        var comment = await CreateTestCommentAsync(userId, bug.Id, "Test comment");
        var commentAttachment = await CreateTestCommentAttachmentAsync(userId, comment.Id, "comment_file.pdf");

        // Act
        var result = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(report.Id, result.Id);

        // Проверяем участников
        Assert.Contains(participant1, result.ParticipantsUserIds);

        // Проверяем баги
        Assert.NotNull(result.Bugs);
        Assert.Single(result.Bugs);
        var bugResult = result.Bugs[0];
        Assert.Equal(bug.Id, bugResult.Id);

        // Проверяем вложения бага
        Assert.NotNull(bugResult.Attachments);
        Assert.Single(bugResult.Attachments);
        Assert.Equal(bugAttachment.Id, bugResult.Attachments[0].Id);

        // Проверяем шаги бага
        Assert.NotNull(bugResult.Steps);
        Assert.Single(bugResult.Steps);
        var stepResult = bugResult.Steps[0];
        Assert.Equal(bugStep.Id, stepResult.Id);
        Assert.Equal("Step to reproduce", stepResult.Text);

        // Проверяем вложения шага
        Assert.NotNull(stepResult.Attachments);
        Assert.Single(stepResult.Attachments);
        Assert.Equal(stepAttachment.Id, stepResult.Attachments[0].Id);

        // Проверяем комментарии
        Assert.NotNull(bugResult.Comments);
        Assert.Single(bugResult.Comments);
        var commentResult = bugResult.Comments[0];
        Assert.Equal(comment.Id, commentResult.Id);

        // Проверяем вложения комментария
        Assert.NotNull(commentResult.Attachments);
        Assert.Single(commentResult.Attachments);
        Assert.Equal(commentAttachment.Id, commentResult.Attachments[0].Id);
    }

    #region Helper Methods

    private async Task<Bugget.Entities.DbModels.Report.ReportSummaryDbModel> CreateTestReportAsync(
        string userId,
        string? organizationId = null)
    {
        var reportDto = new ReportCreateDto
        {
            Title = $"Test Report {Guid.NewGuid()}"
        };
        return await _reportsDbClient.CreateReportAsync(userId, null, organizationId, reportDto);
    }

    private async Task<Bugget.Entities.DbModels.Bug.BugSummaryDbModel> CreateTestBugAsync(
        string userId,
        int reportId,
        string? receive = null,
        string? expect = null,
        string? organizationId = null)
    {
        var bugDto = new BugDto
        {
            Receive = receive ?? "Test bug receive",
            Expect = expect ?? "Test bug expect"
        };
        return await _bugsDbClient.CreateBugAsync(userId, reportId, bugDto);
    }

    private async Task<Bugget.Entities.DbModels.Comment.CommentSummaryDbModel> CreateTestCommentAsync(
        string userId,
        int bugId,
        string text,
        int creatorType = (int)CreatorType.User)
    {
        return await _commentsDbClient.CreateCommentAsync(userId, bugId, text, creatorType);
    }

    private async Task<AttachmentDbModel> CreateTestBugAttachmentAsync(
        string userId,
        int bugId,
        string fileName)
    {
        var createModel = new CreateAttachmentDbModel
        {
            EntityId = bugId,
            AttachType = AttachType_BugFact,
            StorageKey = $"test/bug_{bugId}_{Guid.NewGuid()}.jpg",
            StorageKind = 1,
            CreatorUserId = userId,
            LengthBytes = 1024,
            FileName = fileName,
            MimeType = "image/jpeg"
        };
        return await _attachmentDbClient.CreateAttachment(createModel);
    }

    private async Task<AttachmentDbModel> CreateTestCommentAttachmentAsync(
        string userId,
        int commentId,
        string fileName)
    {
        var createModel = new CreateAttachmentDbModel
        {
            EntityId = commentId,
            AttachType = AttachType_Comment,
            StorageKey = $"test/comment_{commentId}_{Guid.NewGuid()}.pdf",
            StorageKind = 1,
            CreatorUserId = userId,
            LengthBytes = 2048,
            FileName = fileName,
            MimeType = "application/pdf"
        };
        return await _attachmentDbClient.CreateAttachment(createModel);
    }

    private async Task<BugStepSummaryDbModel> CreateTestBugStepAsync(
        string userId,
        int bugId,
        string text)
    {
        return await _bugStepsDbClient.CreateBugStepAsync(userId, bugId, new BugStepDto { Text = text });
    }

    private async Task<AttachmentDbModel> CreateTestBugStepAttachmentAsync(
        string userId,
        int stepId,
        string fileName)
    {
        var createModel = new CreateAttachmentDbModel
        {
            EntityId = stepId,
            AttachType = AttachType_BugStep,
            StorageKey = $"test/step_{stepId}_{Guid.NewGuid()}.jpg",
            StorageKind = 1,
            CreatorUserId = userId,
            LengthBytes = 1024,
            FileName = fileName,
            MimeType = "image/jpeg"
        };
        return await _attachmentDbClient.CreateAttachment(createModel);
    }

    #endregion
}

