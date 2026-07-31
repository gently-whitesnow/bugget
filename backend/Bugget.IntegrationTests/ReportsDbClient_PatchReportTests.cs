using Bugget.BO.Ports;
using Bugget.Entities.DTO.Report;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class ReportsDbClient_PatchReportTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IReportsDbClient _reportsDbClient;

    public ReportsDbClient_PatchReportTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _reportsDbClient = scope.ServiceProvider.GetRequiredService<IReportsDbClient>();
    }

    [Fact(DisplayName = "Обновление отчета меняет title и status")]
    public async Task PatchReportAsync_UpdateTitleAndStatus_ShouldUpdateReport()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var patchDto = new ReportPatchDto { Title = "Updated title", Status = 2 };
        var initialUpdatedAt = report.UpdatedAt;

        await Task.Delay(10);

        // Act
        var result = await _reportsDbClient.PatchReportAsync(report.Id, patchDto);

        // Assert
        Assert.Equal(report.Id, result.Id);
        Assert.Equal("Updated title", result.Title);
        Assert.Equal(2, result.Status);
        Assert.Equal(report.ResponsibleUserId, result.ResponsibleUserId);
        Assert.Equal(report.PastResponsibleUserId, result.PastResponsibleUserId);
        Assert.True(result.UpdatedAt > initialUpdatedAt);
    }

    [Fact(DisplayName = "Обновление responsible_user_id сохраняет past_responsible_user_id")]
    public async Task PatchReportAsync_UpdateResponsibleUser_ShouldUpdatePastResponsible()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var newResponsibleUserId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var patchDto = new ReportPatchDto { ResponsibleUserId = newResponsibleUserId };

        // Act
        var result = await _reportsDbClient.PatchReportAsync(report.Id, patchDto);

        // Assert
        Assert.Equal(report.Id, result.Id);
        Assert.Equal(newResponsibleUserId, result.ResponsibleUserId);
        Assert.Equal(userId, result.PastResponsibleUserId);
    }

    [Fact(DisplayName = "Пустой patch обновляет только updated_at")]
    public async Task PatchReportAsync_NoFields_ShouldUpdateTimestampOnly()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var initialUpdatedAt = report.UpdatedAt;

        await Task.Delay(10);

        // Act
        var result = await _reportsDbClient.PatchReportAsync(report.Id, new ReportPatchDto());

        // Assert
        Assert.Equal(report.Title, result.Title);
        Assert.Equal(report.Status, result.Status);
        Assert.Equal(report.ResponsibleUserId, result.ResponsibleUserId);
        Assert.Equal(report.PastResponsibleUserId, result.PastResponsibleUserId);
        Assert.True(result.UpdatedAt > initialUpdatedAt);
    }

    [Fact(DisplayName = "ChangeStatusAsync обновляет статус отчета")]
    public async Task ChangeStatusAsync_ShouldUpdateReportStatus()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);

        // Act
        await _reportsDbClient.ChangeStatusAsync(report.Id, 3);
        var updated = await _reportsDbClient.GetReportInternalAsync(report.Id);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(3, updated!.Status);
    }

    private async Task<Bugget.Entities.BO.ReportBo.ReportSummary> CreateTestReportAsync(string userId)
    {
        var reportDto = new ReportCreateDto
        {
            Title = $"Test Report {Guid.NewGuid()}"
        };
        return await _reportsDbClient.CreateReportAsync(userId, null, null, reportDto);
    }
}
