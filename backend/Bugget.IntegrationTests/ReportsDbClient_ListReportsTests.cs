using Bugget.DA.Interfaces;
using Bugget.Entities.DbModels.Attachment;
using Bugget.Entities.DTO.Bug;
using Bugget.Entities.DTO.Report;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class ReportsDbClient_ListReportsTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IReportsDbClient _reportsDbClient;
    private readonly IBugsDbClient _bugsDbClient;
    private readonly ICommentsDbClient _commentsDbClient;
    private readonly IParticipantsDbClient _participantsDbClient;
    private readonly IAttachmentDbClient _attachmentDbClient;

    // AttachType константы
    private const int AttachType_BugFact = 0;
    private const int AttachType_Comment = 2;

    public ReportsDbClient_ListReportsTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _reportsDbClient = scope.ServiceProvider.GetRequiredService<IReportsDbClient>();
        _bugsDbClient = scope.ServiceProvider.GetRequiredService<IBugsDbClient>();
        _commentsDbClient = scope.ServiceProvider.GetRequiredService<ICommentsDbClient>();
        _participantsDbClient = scope.ServiceProvider.GetRequiredService<IParticipantsDbClient>();
        _attachmentDbClient = scope.ServiceProvider.GetRequiredService<IAttachmentDbClient>();
    }

    [Fact(DisplayName = "Список пустой - нет репортов")]
    public async Task ListReportsAsync_NoReports_ShouldReturnEmpty()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";

        // Act
        var (total, reports) = await _reportsDbClient.ListReportsAsync(null, userId, null, null, null, 0, 10);

        // Assert
        Assert.Equal(0, total);
        Assert.Empty(reports);
    }

    [Fact(DisplayName = "Получение одного репорта")]
    public async Task ListReportsAsync_OneReport_ShouldReturnOneReport()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);

        // Act
        var (total, reports) = await _reportsDbClient.ListReportsAsync(null, userId, null, null, null, 0, 10);

        // Assert
        Assert.Equal(1, total);
        Assert.Single(reports);
        Assert.Equal(report.Id, reports[0].Id);
        Assert.Equal(report.Title, reports[0].Title);
    }

    [Fact(DisplayName = "Получение нескольких репортов")]
    public async Task ListReportsAsync_MultipleReports_ShouldReturnAll()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report1 = await CreateTestReportAsync(userId, title: "Report 1");
        var report2 = await CreateTestReportAsync(userId, title: "Report 2");
        var report3 = await CreateTestReportAsync(userId, title: "Report 3");

        // Act
        var (total, reports) = await _reportsDbClient.ListReportsAsync(null, userId, null, null, null, 0, 10);

        // Assert
        Assert.Equal(3, total);
        Assert.Equal(3, reports.Length);
        Assert.Contains(reports, r => r.Id == report1.Id);
        Assert.Contains(reports, r => r.Id == report2.Id);
        Assert.Contains(reports, r => r.Id == report3.Id);
    }

    [Fact(DisplayName = "Фильтрация по userId - возвращает только репорты пользователя")]
    public async Task ListReportsAsync_FilterByUserId_ShouldReturnUserReports()
    {
        // Arrange
        var user1 = $"user_{Guid.NewGuid()}";
        var user2 = $"user_{Guid.NewGuid()}";

        var report1 = await CreateTestReportAsync(user1);
        var report2 = await CreateTestReportAsync(user1);
        var report3 = await CreateTestReportAsync(user2); // Другой пользователь

        // Act
        var (total, reports) = await _reportsDbClient.ListReportsAsync(null, user1, null, null, null, 0, 10);

        // Assert
        Assert.Equal(2, total);
        Assert.Equal(2, reports.Length);
        Assert.All(reports, r => Assert.Equal(user1, r.CreatorUserId));
        Assert.DoesNotContain(reports, r => r.Id == report3.Id);
    }

    [Fact(DisplayName = "Фильтрация по teamId - возвращает только репорты команды")]
    public async Task ListReportsAsync_FilterByTeamId_ShouldReturnTeamReports()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var team1 = $"team_{Guid.NewGuid()}";
        var team2 = $"team_{Guid.NewGuid()}";

        var report1 = await CreateTestReportAsync(userId, teamId: team1);
        var report2 = await CreateTestReportAsync(userId, teamId: team1);
        var report3 = await CreateTestReportAsync(userId, teamId: team2); // Другая команда

        // Act
        var (total, reports) = await _reportsDbClient.ListReportsAsync(null, null, team1, null, null, 0, 10);

        // Assert
        Assert.Equal(2, total);
        Assert.Equal(2, reports.Length);
        Assert.All(reports, r => Assert.Equal(team1, r.CreatorTeamId));
        Assert.DoesNotContain(reports, r => r.Id == report3.Id);
    }

    [Fact(DisplayName = "Фильтрация по organizationId - возвращает только репорты организации")]
    public async Task ListReportsAsync_FilterByOrganizationId_ShouldReturnOrgReports()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var org1 = $"org_{Guid.NewGuid()}";
        var org2 = $"org_{Guid.NewGuid()}";

        var report1 = await CreateTestReportAsync(userId, organizationId: org1);
        var report2 = await CreateTestReportAsync(userId, organizationId: org1);
        var report3 = await CreateTestReportAsync(userId, organizationId: org2); // Другая организация

        // Act
        var (total, reports) = await _reportsDbClient.ListReportsAsync(org1, null, null, null, null, 0, 10);

        // Assert
        Assert.Equal(2, total);
        Assert.Equal(2, reports.Length);
        Assert.DoesNotContain(reports, r => r.Id == report3.Id);
    }

    [Fact(DisplayName = "Фильтрация по статусам")]
    public async Task ListReportsAsync_FilterByStatuses_ShouldReturnMatchingReports()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report1 = await CreateTestReportAsync(userId); // Status 0 (Backlog)
        var report2 = await CreateTestReportAsync(userId);

        // Изменяем статус второго репорта
        await _reportsDbClient.PatchReportAsync(report2.Id, new ReportPatchDto { Status = 1 });

        // Act - Ищем только репорты со статусом 0
        var (total, reports) = await _reportsDbClient.ListReportsAsync(null, userId, null, new[] { 0 }, null, 0, 10);

        // Assert
        Assert.Equal(1, total);
        Assert.Single(reports);
        Assert.Equal(report1.Id, reports[0].Id);
        Assert.Equal(0, reports[0].Status);
    }

    [Fact(DisplayName = "Пагинация - skip и take работают корректно")]
    public async Task ListReportsAsync_Pagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        for (int i = 0; i < 5; i++)
        {
            await CreateTestReportAsync(userId, title: $"Report {i}");
        }

        // Act - Пропускаем 2 и берем 2
        var (total, reports) = await _reportsDbClient.ListReportsAsync(null, userId, null, null, null, 2, 2);

        // Assert
        Assert.Equal(5, total); // Всего 5 репортов
        Assert.Equal(2, reports.Length); // Возвращено 2 репорта
    }

    [Fact(DisplayName = "Пагинация - последняя страница может быть неполной")]
    public async Task ListReportsAsync_LastPage_ShouldReturnRemainingReports()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        for (int i = 0; i < 7; i++)
        {
            await CreateTestReportAsync(userId);
        }

        // Act - Пропускаем 5, берем 5 (должно вернуть только 2)
        var (total, reports) = await _reportsDbClient.ListReportsAsync(null, userId, null, null, null, 5, 5);

        // Assert
        Assert.Equal(7, total);
        Assert.Equal(2, reports.Length);
    }

    [Fact(DisplayName = "Репорт с полным графом данных")]
    public async Task ListReportsAsync_CompleteGraph_ShouldReturnFullData()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);

        // Добавляем участника
        var participant = $"user_{Guid.NewGuid()}";
        await _participantsDbClient.AddParticipantIfNotExistAsync(report.Id, participant);

        // Создаем баг с вложением
        var bug = await CreateTestBugAsync(userId, report.Id);

        // Создаем комментарий с вложением
        var comment = await CreateTestCommentAsync(userId, bug.Id, "Test comment");

        // Act
        var (total, reports) = await _reportsDbClient.ListReportsAsync(null, userId, null, null, null, 0, 10);

        // Assert
        Assert.Equal(1, total);
        Assert.Single(reports);

        var result = reports[0];

        // Проверяем участников
        Assert.Contains(participant, result.ParticipantsUserIds);

        // Проверяем баги
        Assert.NotNull(result.Bugs);
        Assert.Single(result.Bugs);
        var bugResult = result.Bugs[0];
        Assert.Equal(bug.Id, bugResult.Id);

        // Проверяем комментарии
        Assert.NotNull(bugResult.Comments);
        Assert.Single(bugResult.Comments);
        var commentResult = bugResult.Comments[0];
        Assert.Equal(comment.Id, commentResult.Id);
    }

    [Fact(DisplayName = "Несколько репортов с разными графами данных")]
    public async Task ListReportsAsync_MultipleReportsWithGraphs_ShouldGroupCorrectly()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";

        // Репорт 1 с одним багом
        var report1 = await CreateTestReportAsync(userId);
        var bug1 = await CreateTestBugAsync(userId, report1.Id, "Bug in Report 1");
        var comment1 = await CreateTestCommentAsync(userId, bug1.Id, "Comment in Report 1");

        // Репорт 2 с двумя багами
        var report2 = await CreateTestReportAsync(userId);
        var bug2a = await CreateTestBugAsync(userId, report2.Id, "Bug 2A");
        var bug2b = await CreateTestBugAsync(userId, report2.Id, "Bug 2B");
        var comment2a = await CreateTestCommentAsync(userId, bug2a.Id, "Comment 2A");

        // Act
        var (total, reports) = await _reportsDbClient.ListReportsAsync(null, userId, null, null, null, 0, 10);

        // Assert
        Assert.Equal(2, total);
        Assert.Equal(2, reports.Length);

        var result1 = reports.First(r => r.Id == report1.Id);
        var result2 = reports.First(r => r.Id == report2.Id);

        // Проверяем что данные не перемешались
        Assert.NotNull(result1.Bugs);
        Assert.Single(result1.Bugs);
        Assert.Equal(bug1.Id, result1.Bugs[0].Id);
        Assert.NotNull(result1.Bugs[0].Comments);
        Assert.Single(result1.Bugs[0].Comments!);
        Assert.Equal(comment1.Id, result1.Bugs[0].Comments![0].Id);

        Assert.NotNull(result2.Bugs);
        Assert.Equal(2, result2.Bugs.Length);
        Assert.Contains(result2.Bugs, b => b.Id == bug2a.Id);
        Assert.Contains(result2.Bugs, b => b.Id == bug2b.Id);
    }

    [Fact(DisplayName = "Репорт без багов имеет пустой массив багов")]
    public async Task ListReportsAsync_EmptyReport_ShouldReturnEmptyBugsArray()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);

        // Act
        var (total, reports) = await _reportsDbClient.ListReportsAsync(null, userId, null, null, null, 0, 10);

        // Assert
        Assert.Single(reports);
        var result = reports[0];

        Assert.NotNull(result.Bugs);
        Assert.Empty(result.Bugs);
        Assert.NotNull(result.ParticipantsUserIds);
        // Примечание: ParticipantsUserIds может содержать создателя или других участников
    }

    [Fact(DisplayName = "Комбинированная фильтрация - userId и статусы")]
    public async Task ListReportsAsync_CombinedFilters_ShouldApplyAll()
    {
        // Arrange
        var user1 = $"user_{Guid.NewGuid()}";
        var user2 = $"user_{Guid.NewGuid()}";

        var report1 = await CreateTestReportAsync(user1); // user1, status 0
        var report2 = await CreateTestReportAsync(user1); // user1, status 0
        await _reportsDbClient.PatchReportAsync(report2.Id, new ReportPatchDto { Status = 1 }); // меняем на status 1
        var report3 = await CreateTestReportAsync(user2); // user2, status 0

        // Act - Ищем репорты user1 со статусом 0
        var (total, reports) = await _reportsDbClient.ListReportsAsync(null, user1, null, new[] { 0 }, null, 0, 10);

        // Assert
        Assert.Equal(1, total);
        Assert.Single(reports);
        Assert.Equal(report1.Id, reports[0].Id);
        Assert.Equal(user1, reports[0].CreatorUserId);
        Assert.Equal(0, reports[0].Status);
    }

    [Fact(DisplayName = "Фильтр creatorTypes=[2] возвращает только tester-authored репорты")]
    public async Task ListReportsAsync_FilterByCreatorTypes_ShouldReturnOnlyMatching()
    {
        // Arrange
        var orgId = $"org_{Guid.NewGuid()}";
        var userId = $"user_{Guid.NewGuid()}";
        var testerId = $"tester_{Guid.NewGuid()}";

        var userReport = await CreateTestReportAsync(userId, organizationId: orgId);
        var testerReport = await CreateTestReportAsync(testerId, organizationId: orgId);
        await SetReportCreatorTypeAsync(testerReport.Id, 2);

        // Act — only tester
        var (totalTester, testerReports) = await _reportsDbClient.ListReportsAsync(orgId, null, null, null, new[] { 2 }, 0, 10);

        // Assert
        Assert.Equal(1, totalTester);
        Assert.Single(testerReports);
        Assert.Equal(testerReport.Id, testerReports[0].Id);

        // Act — only non-tester (User=0, System=1)
        var (totalNonTester, nonTesterReports) = await _reportsDbClient.ListReportsAsync(orgId, null, null, null, new[] { 0, 1 }, 0, 10);

        // Assert
        Assert.Equal(1, totalNonTester);
        Assert.Single(nonTesterReports);
        Assert.Equal(userReport.Id, nonTesterReports[0].Id);

        // Act — null filter returns both
        var (totalAll, allReports) = await _reportsDbClient.ListReportsAsync(orgId, null, null, null, null, 0, 10);

        // Assert
        Assert.Equal(2, totalAll);
        Assert.Equal(2, allReports.Length);
    }

    [Fact(DisplayName = "Take = 0 возвращает пустой массив, но правильный total")]
    public async Task ListReportsAsync_TakeZero_ShouldReturnEmptyWithCorrectTotal()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        await CreateTestReportAsync(userId);
        await CreateTestReportAsync(userId);
        await CreateTestReportAsync(userId);

        // Act
        var (total, reports) = await _reportsDbClient.ListReportsAsync(null, userId, null, null, null, 0, 0);

        // Assert
        Assert.Equal(3, total); // Всего 3 репорта
        Assert.Empty(reports); // Но не возвращено ни одного
    }

    #region Helper Methods

    private async Task<Bugget.Entities.DbModels.Report.ReportSummaryDbModel> CreateTestReportAsync(
        string userId,
        string? teamId = null,
        string? organizationId = null,
        string? title = null)
    {
        var reportDto = new ReportCreateDto
        {
            Title = title ?? $"Test Report {Guid.NewGuid()}"
        };
        return await _reportsDbClient.CreateReportAsync(userId, teamId, organizationId, reportDto);
    }

    private async Task<Bugget.Entities.DbModels.Bug.BugSummaryDbModel> CreateTestBugAsync(
        string userId,
        int reportId,
        string? receive = null)
    {
        var bugDto = new BugDto
        {
            Receive = receive ?? "Test bug receive",
            Expect = "Test bug expect"
        };
        return await _bugsDbClient.CreateBugAsync(userId, reportId, bugDto);
    }

    private async Task<Bugget.Entities.DbModels.Comment.CommentSummaryDbModel> CreateTestCommentAsync(
        string userId,
        int bugId,
        string text)
    {
        return await _commentsDbClient.CreateCommentAsync(userId, bugId, text);
    }

    private static async Task SetReportCreatorTypeAsync(int reportId, short creatorType)
    {
        var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
        await using var ds = NpgsqlDataSource.Create(connString);
        await using var conn = await ds.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.reports SET creator_type = @ct WHERE id = @id";
        cmd.Parameters.AddWithValue("@ct", creatorType);
        cmd.Parameters.AddWithValue("@id", reportId);
        await cmd.ExecuteNonQueryAsync();
    }

    #endregion
}

