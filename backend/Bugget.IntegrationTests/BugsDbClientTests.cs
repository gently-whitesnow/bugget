using Bugget.Application.Commands.Bug;
using Bugget.Application.Commands.Report;
using Bugget.Application.Ports;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class BugsDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IBugsDbClient _bugsDbClient;
    private readonly IReportsDbClient _reportsDbClient;

    public BugsDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _bugsDbClient = scope.ServiceProvider.GetRequiredService<IBugsDbClient>();
        _reportsDbClient = scope.ServiceProvider.GetRequiredService<IReportsDbClient>();
    }

    #region CreateBugAsync Tests

    [Fact(DisplayName = "Успешное создание бага с минимальными параметрами")]
    public async Task CreateBugAsync_WithMinimalParameters_ShouldCreateBug()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bugDto = new BugDto
        {
            Receive = "Unexpected error",
            Expect = "Success message"
        };

        var result = await _bugsDbClient.CreateBugAsync(userId, report.Id, bugDto);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(bugDto.Receive, result.Receive);
        Assert.Equal(bugDto.Expect, result.Expect);
        Assert.Equal(userId, result.CreatorUserId);
        Assert.True(result.CreatedAt > DateTimeOffset.MinValue);
        Assert.True(result.UpdatedAt > DateTimeOffset.MinValue);
        Assert.Equal(0, result.Status);
    }

    [Fact(DisplayName = "Успешное создание бага с organizationId")]
    public async Task CreateBugAsync_WithOrganizationId_ShouldCreateBug()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var organizationId = $"org_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId, organizationId);
        var bugDto = new BugDto
        {
            Receive = "Error 500",
            Expect = "Success response"
        };

        var result = await _bugsDbClient.CreateBugAsync(userId, report.Id, bugDto);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(bugDto.Receive, result.Receive);
        Assert.Equal(bugDto.Expect, result.Expect);
        Assert.Equal(userId, result.CreatorUserId);
        Assert.Equal(0, result.Status);
    }

    [Fact(DisplayName = "Успешное создание бага только с Receive")]
    public async Task CreateBugAsync_WithOnlyReceive_ShouldCreateBug()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bugDto = new BugDto
        {
            Receive = "Something went wrong",
            Expect = null
        };

        var result = await _bugsDbClient.CreateBugAsync(userId, report.Id, bugDto);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(bugDto.Receive, result.Receive);
        Assert.Null(result.Expect);
        Assert.Equal(userId, result.CreatorUserId);
    }

    [Fact(DisplayName = "Успешное создание бага только с Expect")]
    public async Task CreateBugAsync_WithOnlyExpect_ShouldCreateBug()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bugDto = new BugDto
        {
            Receive = null,
            Expect = "Expected success"
        };

        var result = await _bugsDbClient.CreateBugAsync(userId, report.Id, bugDto);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Null(result.Receive);
        Assert.Equal(bugDto.Expect, result.Expect);
        Assert.Equal(userId, result.CreatorUserId);
    }

    [Fact(DisplayName = "Создание нескольких багов для одного репорта")]
    public async Task CreateBugAsync_MultipleBugsForOneReport_ShouldCreateSeparateBugs()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bugDto1 = new BugDto { Receive = "First bug", Expect = "First fix" };
        var bugDto2 = new BugDto { Receive = "Second bug", Expect = "Second fix" };

        var result1 = await _bugsDbClient.CreateBugAsync(userId, report.Id, bugDto1);
        var result2 = await _bugsDbClient.CreateBugAsync(userId, report.Id, bugDto2);

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotEqual(result1.Id, result2.Id);
        Assert.Equal("First bug", result1.Receive);
        Assert.Equal("Second bug", result2.Receive);
        Assert.Equal(userId, result1.CreatorUserId);
        Assert.Equal(userId, result2.CreatorUserId);
    }

    [Fact(DisplayName = "Проверка что начальный статус бага равен 0")]
    public async Task CreateBugAsync_ShouldSetStatusToZero()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bugDto = new BugDto
        {
            Receive = "Test bug",
            Expect = "Test expectation"
        };

        var result = await _bugsDbClient.CreateBugAsync(userId, report.Id, bugDto);

        Assert.NotNull(result);
        Assert.Equal(0, result.Status);
    }

    [Fact(DisplayName = "Создание бага с длинным текстом Receive")]
    public async Task CreateBugAsync_WithLongReceive_ShouldCreateBug()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var longReceive = new string('a', 2048); // Максимальная длина
        var bugDto = new BugDto
        {
            Receive = longReceive,
            Expect = "Fix needed"
        };

        var result = await _bugsDbClient.CreateBugAsync(userId, report.Id, bugDto);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(longReceive, result.Receive);
    }

    [Fact(DisplayName = "Создание бага с длинным текстом Expect")]
    public async Task CreateBugAsync_WithLongExpect_ShouldCreateBug()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var longExpect = new string('b', 2048); // Максимальная длина
        var bugDto = new BugDto
        {
            Receive = "Bug found",
            Expect = longExpect
        };

        var result = await _bugsDbClient.CreateBugAsync(userId, report.Id, bugDto);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(longExpect, result.Expect);
    }

    #endregion

    #region PatchBugAsync Tests

    [Fact(DisplayName = "Успешное обновление Receive бага")]
    public async Task PatchBugAsync_UpdateReceive_ShouldUpdateBug()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var patchDto = new BugPatchDto
        {
            Receive = "Updated receive text"
        };

        var result = await _bugsDbClient.PatchBugAsync(report.Id, bug.Id, patchDto);

        Assert.NotNull(result);
        Assert.Equal(bug.Id, result.Id);
        Assert.Equal(patchDto.Receive, result.Receive);
        Assert.Equal(bug.Expect, result.Expect);
        Assert.True(result.UpdatedAt > bug.UpdatedAt);
    }

    [Fact(DisplayName = "Успешное обновление Expect бага")]
    public async Task PatchBugAsync_UpdateExpect_ShouldUpdateBug()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var patchDto = new BugPatchDto
        {
            Expect = "Updated expect text"
        };

        var result = await _bugsDbClient.PatchBugAsync(report.Id, bug.Id, patchDto);

        Assert.NotNull(result);
        Assert.Equal(bug.Id, result.Id);
        Assert.Equal(bug.Receive, result.Receive);
        Assert.Equal(patchDto.Expect, result.Expect);
        Assert.True(result.UpdatedAt > bug.UpdatedAt);
    }

    [Fact(DisplayName = "Успешное обновление Status бага")]
    public async Task PatchBugAsync_UpdateStatus_ShouldUpdateBug()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var patchDto = new BugPatchDto
        {
            Status = 1
        };

        var result = await _bugsDbClient.PatchBugAsync(report.Id, bug.Id, patchDto);

        Assert.NotNull(result);
        Assert.Equal(bug.Id, result.Id);
        Assert.Equal(1, result.Status);
        Assert.Equal(bug.Receive, result.Receive);
        Assert.Equal(bug.Expect, result.Expect);
        Assert.True(result.UpdatedAt > bug.UpdatedAt);
    }

    [Fact(DisplayName = "Успешное обновление всех полей бага")]
    public async Task PatchBugAsync_UpdateAllFields_ShouldUpdateBug()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var patchDto = new BugPatchDto
        {
            Receive = "Completely new receive",
            Expect = "Completely new expect",
            Status = 2
        };

        var result = await _bugsDbClient.PatchBugAsync(report.Id, bug.Id, patchDto);

        Assert.NotNull(result);
        Assert.Equal(bug.Id, result.Id);
        Assert.Equal(patchDto.Receive, result.Receive);
        Assert.Equal(patchDto.Expect, result.Expect);
        Assert.Equal(patchDto.Status, result.Status);
        Assert.True(result.UpdatedAt > bug.UpdatedAt);
    }

    [Fact(DisplayName = "Обновление бага с organizationId")]
    public async Task PatchBugAsync_WithOrganizationId_ShouldUpdateBug()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var organizationId = $"org_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId, organizationId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var patchDto = new BugPatchDto
        {
            Receive = "Updated with org"
        };

        var result = await _bugsDbClient.PatchBugAsync(report.Id, bug.Id, patchDto);

        Assert.NotNull(result);
        Assert.Equal(bug.Id, result.Id);
        Assert.Equal(patchDto.Receive, result.Receive);
    }

    [Fact(DisplayName = "Обновление бага с пустыми строками")]
    public async Task PatchBugAsync_WithEmptyStrings_ShouldNotUpdateFields()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var patchDto = new BugPatchDto
        {
            Receive = null,
            Expect = null,
            Status = null
        };

        var result = await _bugsDbClient.PatchBugAsync(report.Id, bug.Id, patchDto);

        Assert.NotNull(result);
        Assert.Equal(bug.Id, result.Id);
        // Проверяем что поля не изменились (значения остались прежними)
        Assert.Equal(bug.Receive, result.Receive);
        Assert.Equal(bug.Expect, result.Expect);
        Assert.Equal(bug.Status, result.Status);
    }

    [Fact(DisplayName = "Обновление UpdatedAt при каждом патче")]
    public async Task PatchBugAsync_ShouldUpdateTimestamp()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var initialUpdatedAt = bug.UpdatedAt;

        // Небольшая задержка для гарантии различия времени
        await Task.Delay(10);

        var result = await _bugsDbClient.PatchBugAsync(
            report.Id,
            bug.Id,
            new BugPatchDto { Receive = "New text" }
        );

        Assert.True(result.UpdatedAt > initialUpdatedAt);
    }

    #endregion

    #region GetBugAsync Tests

    [Fact(DisplayName = "Получение бага по reportId и bugId возвращает баг")]
    public async Task GetBugAsync_WithValidIds_ShouldReturnBug()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);

        var result = await _bugsDbClient.GetBugAsync(report.Id, bug.Id);

        Assert.NotNull(result);
        Assert.Equal(bug.Id, result!.Id);
        Assert.Equal(bug.Receive, result.Receive);
        Assert.Equal(bug.Expect, result.Expect);
        Assert.Equal(bug.CreatorUserId, result.CreatorUserId);
        Assert.Equal(bug.Status, result.Status);
        Assert.True(result.CreatedAt > DateTimeOffset.MinValue);
        Assert.True(result.UpdatedAt > DateTimeOffset.MinValue);
    }

    [Fact(DisplayName = "Получение бага с неверным reportId возвращает null")]
    public async Task GetBugAsync_WithWrongReportId_ShouldReturnNull()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var otherReport = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);

        var result = await _bugsDbClient.GetBugAsync(otherReport.Id, bug.Id);

        Assert.Null(result);
    }

    [Fact(DisplayName = "Получение несуществующего бага возвращает null")]
    public async Task GetBugAsync_NonExistentBug_ShouldReturnNull()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var nonExistentBugId = int.MaxValue;

        var result = await _bugsDbClient.GetBugAsync(report.Id, nonExistentBugId);

        Assert.Null(result);
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

    #endregion
}
