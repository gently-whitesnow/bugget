using Bugget.BO.Ports;
using Bugget.Entities.DTO.Bug;
using Bugget.Entities.DTO.BugStep;
using Bugget.Entities.DTO.Report;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class BugStepsDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IBugStepsDbClient _bugStepsDbClient;
    private readonly IBugsDbClient _bugsDbClient;
    private readonly IReportsDbClient _reportsDbClient;

    public BugStepsDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _bugStepsDbClient = scope.ServiceProvider.GetRequiredService<IBugStepsDbClient>();
        _bugsDbClient = scope.ServiceProvider.GetRequiredService<IBugsDbClient>();
        _reportsDbClient = scope.ServiceProvider.GetRequiredService<IReportsDbClient>();
    }

    [Fact(DisplayName = "Успешное создание шага с минимальными параметрами")]
    public async Task CreateBugStepAsync_WithMinimalParameters_ShouldCreateBugStep()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var stepDto = new BugStepDto
        {
            Text = "Step 1: Open the application"
        };

        // Act
        var result = await _bugStepsDbClient.CreateBugStepAsync(userId, bug.Id, stepDto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(bug.Id, result.BugId);
        Assert.Equal(stepDto.Text, result.Text);
        Assert.Equal(1, result.StepNumber); // Первый шаг должен иметь номер 1
        Assert.Equal(userId, result.CreatorUserId);
        Assert.True(result.CreatedAt > DateTimeOffset.MinValue);
        Assert.True(result.UpdatedAt > DateTimeOffset.MinValue);
        Assert.Equal(result.CreatedAt, result.UpdatedAt); // При создании времена должны совпадать
    }

    [Fact(DisplayName = "Успешное создание шага с organizationId")]
    public async Task CreateBugStepAsync_WithOrganizationId_ShouldCreateBugStep()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var organizationId = $"org_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId, organizationId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var stepDto = new BugStepDto
        {
            Text = "Step with organization"
        };

        // Act
        var result = await _bugStepsDbClient.CreateBugStepAsync(userId, bug.Id, stepDto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(bug.Id, result.BugId);
        Assert.Equal(stepDto.Text, result.Text);
        Assert.Equal(1, result.StepNumber);
        Assert.Equal(userId, result.CreatorUserId);
    }

    [Fact(DisplayName = "Создание нескольких шагов для одного бага")]
    public async Task CreateBugStepAsync_MultipleStepsForOneBug_ShouldCreateSeparateSteps()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var stepDto1 = new BugStepDto { Text = "Step 1: Open app" };
        var stepDto2 = new BugStepDto { Text = "Step 2: Click button" };
        var stepDto3 = new BugStepDto { Text = "Step 3: Verify result" };

        // Act
        var result1 = await _bugStepsDbClient.CreateBugStepAsync(userId, bug.Id, stepDto1);
        var result2 = await _bugStepsDbClient.CreateBugStepAsync(userId, bug.Id, stepDto2);
        var result3 = await _bugStepsDbClient.CreateBugStepAsync(userId, bug.Id, stepDto3);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        Assert.NotEqual(result1.Id, result2.Id);
        Assert.NotEqual(result2.Id, result3.Id);
        Assert.NotEqual(result1.Id, result3.Id);
        Assert.Equal(1, result1.StepNumber);
        Assert.Equal(2, result2.StepNumber);
        Assert.Equal(3, result3.StepNumber);
        Assert.Equal(stepDto1.Text, result1.Text);
        Assert.Equal(stepDto2.Text, result2.Text);
        Assert.Equal(stepDto3.Text, result3.Text);
        Assert.Equal(bug.Id, result1.BugId);
        Assert.Equal(bug.Id, result2.BugId);
        Assert.Equal(bug.Id, result3.BugId);
    }

    [Fact(DisplayName = "Создание шагов разными пользователями")]
    public async Task CreateBugStepAsync_DifferentUsers_ShouldCreateSteps()
    {
        // Arrange
        var user1 = $"user_{Guid.NewGuid()}";
        var user2 = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(user1);
        var bug = await CreateTestBugAsync(user1, report.Id);

        // Act
        var step1 = await _bugStepsDbClient.CreateBugStepAsync(user1, bug.Id, new BugStepDto { Text = "Step by user1" });
        var step2 = await _bugStepsDbClient.CreateBugStepAsync(user2, bug.Id, new BugStepDto { Text = "Step by user2" });

        // Assert
        Assert.NotNull(step1);
        Assert.NotNull(step2);
        Assert.NotEqual(step1.Id, step2.Id);
        Assert.Equal(user1, step1.CreatorUserId);
        Assert.Equal(user2, step2.CreatorUserId);
        Assert.Equal(bug.Id, step1.BugId);
        Assert.Equal(bug.Id, step2.BugId);
    }

    [Fact(DisplayName = "Успешное обновление текста шага")]
    public async Task PatchBugStepAsync_WithNewText_ShouldUpdateBugStep()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step = await CreateTestBugStepAsync(userId, bug.Id, "Original text");
        var patchDto = new BugStepDto
        {
            Text = "Updated text"
        };

        // Act
        var result = await _bugStepsDbClient.PatchBugStepInternalAsync(report.Id, bug.Id, step.Id, patchDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(step.Id, result.Id);
        Assert.Equal(patchDto.Text, result.Text);
        Assert.Equal(bug.Id, result.BugId);
        Assert.Equal(step.StepNumber, result.StepNumber); // Номер шага не должен измениться
        Assert.Equal(userId, result.CreatorUserId);
        Assert.True(result.UpdatedAt > step.UpdatedAt);
        Assert.Equal(step.CreatedAt, result.CreatedAt); // CreatedAt не должен измениться
    }

    [Fact(DisplayName = "Обновление шага с organizationId")]
    public async Task PatchBugStepAsync_WithOrganizationId_ShouldUpdateBugStep()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var organizationId = $"org_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId, organizationId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step = await CreateTestBugStepAsync(userId, bug.Id, "Original");
        var patchDto = new BugStepDto
        {
            Text = "Updated with org"
        };

        // Act
        var result = await _bugStepsDbClient.PatchBugStepInternalAsync(report.Id, bug.Id, step.Id, patchDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(step.Id, result.Id);
        Assert.Equal(patchDto.Text, result.Text);
    }

    [Fact(DisplayName = "Успешное удаление шага")]
    public async Task DeleteBugStepAsync_WithValidStep_ShouldDeleteStep()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step = await CreateTestBugStepAsync(userId, bug.Id, "To be deleted");

        // Act
        var result = await _bugStepsDbClient.DeleteBugStepInternalAsync(report.Id, bug.Id, step.Id);

        // Assert
        Assert.NotNull(result);
    }

    [Fact(DisplayName = "Удаление шага с organizationId")]
    public async Task DeleteBugStepAsync_WithOrganizationId_ShouldDeleteStep()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var organizationId = $"org_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId, organizationId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step = await CreateTestBugStepAsync(userId, bug.Id, "To be deleted");

        // Act
        var result = await _bugStepsDbClient.DeleteBugStepInternalAsync(report.Id, bug.Id, step.Id);

        // Assert
        Assert.NotNull(result);
    }

    [Fact(DisplayName = "Удаление одного из нескольких шагов")]
    public async Task DeleteBugStepAsync_OneOfMultiple_ShouldDeleteOnlyOne()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step1 = await CreateTestBugStepAsync(userId, bug.Id, "Step 1");
        var step2 = await CreateTestBugStepAsync(userId, bug.Id, "Step 2");
        var step3 = await CreateTestBugStepAsync(userId, bug.Id, "Step 3");

        // Act - удаляем второй шаг
        var result = await _bugStepsDbClient.DeleteBugStepInternalAsync(report.Id, bug.Id, step2.Id);

        // Assert - проверяем что step2 удален
        Assert.NotNull(result);

        // Assert - проверяем что step1 и step3 все еще существуют
        var updateResult1 = await _bugStepsDbClient.PatchBugStepInternalAsync(report.Id, bug.Id, step1.Id, new BugStepDto { Text = "Updated 1" });
        var updateResult3 = await _bugStepsDbClient.PatchBugStepInternalAsync(report.Id, bug.Id, step3.Id, new BugStepDto { Text = "Updated 3" });
        Assert.NotNull(updateResult1);
        Assert.NotNull(updateResult3);
        Assert.Equal("Updated 1", updateResult1.Text);
        Assert.Equal("Updated 3", updateResult3.Text);
    }

    [Fact(DisplayName = "Удаление несуществующего шага не должно вернуть null")]
    public async Task DeleteBugStepAsync_NonExistentStep_ShouldReturnNull()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var nonExistentStepId = 999999;

        // Act
        await _bugStepsDbClient.DeleteBugStepInternalAsync(report.Id, bug.Id, nonExistentStepId);
    }

    [Fact(DisplayName = "Успешное изменение порядка шагов")]
    public async Task UpdateBugStepsOrderAsync_WithValidOrder_ShouldUpdateOrder()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step1 = await CreateTestBugStepAsync(userId, bug.Id, "Step 1");
        var step2 = await CreateTestBugStepAsync(userId, bug.Id, "Step 2");
        var step3 = await CreateTestBugStepAsync(userId, bug.Id, "Step 3");

        // Изначальный порядок: step1 (1), step2 (2), step3 (3)
        // Меняем на обратный: step3 (1), step2 (2), step1 (3)
        var orderDto = new BugStepsOrderDto
        {
            StepIds = new[] { step3.Id, step2.Id, step1.Id }
        };

        // Act
        var result = await _bugStepsDbClient.UpdateBugStepsOrderInternalAsync(report.Id, bug.Id, orderDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Equal(step3.Id, result[0].Id);
        Assert.Equal(step2.Id, result[1].Id);
        Assert.Equal(step1.Id, result[2].Id);
        Assert.Equal(1, result[0].StepNumber);
        Assert.Equal(2, result[1].StepNumber);
        Assert.Equal(3, result[2].StepNumber);
    }

    [Fact(DisplayName = "Изменение порядка шагов с organizationId")]
    public async Task UpdateBugStepsOrderAsync_WithOrganizationId_ShouldUpdateOrder()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var organizationId = $"org_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId, organizationId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step1 = await CreateTestBugStepAsync(userId, bug.Id, "Step 1");
        var step2 = await CreateTestBugStepAsync(userId, bug.Id, "Step 2");

        var orderDto = new BugStepsOrderDto
        {
            StepIds = new[] { step2.Id, step1.Id }
        };

        // Act
        var result = await _bugStepsDbClient.UpdateBugStepsOrderInternalAsync(report.Id, bug.Id, orderDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.Equal(step2.Id, result[0].Id);
        Assert.Equal(step1.Id, result[1].Id);
        Assert.Equal(1, result[0].StepNumber);
        Assert.Equal(2, result[1].StepNumber);
    }

    [Fact(DisplayName = "Изменение порядка одного шага")]
    public async Task UpdateBugStepsOrderAsync_SingleStep_ShouldUpdateOrder()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step1 = await CreateTestBugStepAsync(userId, bug.Id, "Step 1");

        var orderDto = new BugStepsOrderDto
        {
            StepIds = new[] { step1.Id }
        };

        // Act
        var result = await _bugStepsDbClient.UpdateBugStepsOrderInternalAsync(report.Id, bug.Id, orderDto);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(step1.Id, result[0].Id);
        Assert.Equal(1, result[0].StepNumber);
    }

    [Fact(DisplayName = "Изменение порядка всех шагов в обратном порядке")]
    public async Task UpdateBugStepsOrderAsync_ReverseOrder_ShouldUpdateOrder()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step1 = await CreateTestBugStepAsync(userId, bug.Id, "Step 1");
        var step2 = await CreateTestBugStepAsync(userId, bug.Id, "Step 2");
        var step3 = await CreateTestBugStepAsync(userId, bug.Id, "Step 3");
        var step4 = await CreateTestBugStepAsync(userId, bug.Id, "Step 4");

        // Меняем порядок на обратный
        var orderDto = new BugStepsOrderDto
        {
            StepIds = new[] { step4.Id, step3.Id, step2.Id, step1.Id }
        };

        // Act
        var result = await _bugStepsDbClient.UpdateBugStepsOrderInternalAsync(report.Id, bug.Id, orderDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Length);
        Assert.Equal(step4.Id, result[0].Id);
        Assert.Equal(step3.Id, result[1].Id);
        Assert.Equal(step2.Id, result[2].Id);
        Assert.Equal(step1.Id, result[3].Id);
        Assert.Equal(1, result[0].StepNumber);
        Assert.Equal(2, result[1].StepNumber);
        Assert.Equal(3, result[2].StepNumber);
        Assert.Equal(4, result[3].StepNumber);
    }

    [Fact(DisplayName = "Изменение порядка части шагов")]
    public async Task UpdateBugStepsOrderAsync_PartialOrder_ShouldUpdateOrder()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step1 = await CreateTestBugStepAsync(userId, bug.Id, "Step 1");
        var step2 = await CreateTestBugStepAsync(userId, bug.Id, "Step 2");
        var step3 = await CreateTestBugStepAsync(userId, bug.Id, "Step 3");

        // Меняем порядок только первых двух
        var orderDto = new BugStepsOrderDto
        {
            StepIds = new[] { step2.Id, step1.Id, step3.Id }
        };

        // Act
        var result = await _bugStepsDbClient.UpdateBugStepsOrderInternalAsync(report.Id, bug.Id, orderDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Equal(step2.Id, result[0].Id);
        Assert.Equal(step1.Id, result[1].Id);
        Assert.Equal(step3.Id, result[2].Id);
    }

    [Fact(DisplayName = "Получение списка шагов без шагов возвращает пустой массив")]
    public async Task ListBugStepsInternalAsync_NoSteps_ShouldReturnEmpty()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);

        // Act
        var result = await _bugStepsDbClient.ListBugStepsInternalAsync(report.Id, bug.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact(DisplayName = "Получение списка шагов возвращает шаги в порядке step_number")]
    public async Task ListBugStepsInternalAsync_WithSteps_ShouldReturnOrderedSteps()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var bug = await CreateTestBugAsync(userId, report.Id);
        var step1 = await CreateTestBugStepAsync(userId, bug.Id, "Step 1");
        var step2 = await CreateTestBugStepAsync(userId, bug.Id, "Step 2");
        var step3 = await CreateTestBugStepAsync(userId, bug.Id, "Step 3");

        // Act
        var result = await _bugStepsDbClient.ListBugStepsInternalAsync(report.Id, bug.Id);

        // Assert
        Assert.Equal(3, result.Length);
        Assert.Equal(step1.Id, result[0].Id);
        Assert.Equal(step2.Id, result[1].Id);
        Assert.Equal(step3.Id, result[2].Id);
        Assert.Equal(1, result[0].StepNumber);
        Assert.Equal(2, result[1].StepNumber);
        Assert.Equal(3, result[2].StepNumber);
    }

    private async Task<Bugget.Entities.BO.ReportBo.ReportSummary> CreateTestReportAsync(
        string userId,
        string? organizationId = null)
    {
        var reportDto = new ReportCreateDto
        {
            Title = $"Test Report {Guid.NewGuid()}"
        };
        return await _reportsDbClient.CreateReportAsync(userId, null, organizationId, reportDto);
    }

    private async Task<Bugget.Entities.BO.Bugs.BugSummary> CreateTestBugAsync(
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

    private async Task<Bugget.Entities.BO.Bugs.BugStepSummary> CreateTestBugStepAsync(
        string userId,
        int bugId,
        string text)
    {
        return await _bugStepsDbClient.CreateBugStepAsync(userId, bugId, new BugStepDto { Text = text });
    }
}
