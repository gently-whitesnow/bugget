using Bugget.DA.Interfaces;
using Bugget.Entities.DTO.Link;
using Bugget.Entities.DTO.Report;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class ReportLinksDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IReportLinksDbClient _reportLinksDbClient;
    private readonly IReportsDbClient _reportsDbClient;

    public ReportLinksDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _reportLinksDbClient = scope.ServiceProvider.GetRequiredService<IReportLinksDbClient>();
        _reportsDbClient = scope.ServiceProvider.GetRequiredService<IReportsDbClient>();
    }

    #region CreateReportLinkAsync Tests

    [Fact(DisplayName = "Успешное создание ссылки с минимальными параметрами")]
    public async Task CreateReportLinkAsync_WithMinimalParameters_ShouldCreateLink()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var dto = new ReportLinkDto
        {
            Link = "https://example.com",
            Name = "Example Link"
        };

        // Act
        var result = await _reportLinksDbClient.CreateReportLinkInternalAsync(report.Id, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(report.Id, result.ReportId);
        Assert.Equal(dto.Link, result.Link);
        Assert.Equal(dto.Name, result.Name);
        Assert.True(result.CreatedAt > DateTimeOffset.MinValue);
        Assert.True(result.UpdatedAt > DateTimeOffset.MinValue);
        Assert.Equal(result.CreatedAt, result.UpdatedAt); // При создании времена должны совпадать
    }

    [Fact(DisplayName = "Успешное создание ссылки с organizationId")]
    public async Task CreateReportLinkAsync_WithOrganizationId_ShouldCreateLink()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var organizationId = $"org_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId, organizationId);
        var dto = new ReportLinkDto
        {
            Link = "https://example.com/org",
            Name = "Organization Link"
        };

        // Act
        var result = await _reportLinksDbClient.CreateReportLinkInternalAsync(report.Id, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(report.Id, result.ReportId);
        Assert.Equal(dto.Link, result.Link);
        Assert.Equal(dto.Name, result.Name);
    }

    [Fact(DisplayName = "Создание нескольких ссылок к одному репорту")]
    public async Task CreateReportLinkAsync_MultipleLinksForOneReport_ShouldCreateSeparateLinks()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var dto1 = new ReportLinkDto { Link = "https://link1.com", Name = "Link 1" };
        var dto2 = new ReportLinkDto { Link = "https://link2.com", Name = "Link 2" };
        var dto3 = new ReportLinkDto { Link = "https://link3.com", Name = "Link 3" };

        // Act
        var result1 = await _reportLinksDbClient.CreateReportLinkInternalAsync(report.Id, dto1);
        var result2 = await _reportLinksDbClient.CreateReportLinkInternalAsync(report.Id, dto2);
        var result3 = await _reportLinksDbClient.CreateReportLinkInternalAsync(report.Id, dto3);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        Assert.NotEqual(result1.Id, result2.Id);
        Assert.NotEqual(result2.Id, result3.Id);
        Assert.NotEqual(result1.Id, result3.Id);
        Assert.Equal(dto1.Link, result1.Link);
        Assert.Equal(dto2.Link, result2.Link);
        Assert.Equal(dto3.Link, result3.Link);
        Assert.Equal(report.Id, result1.ReportId);
        Assert.Equal(report.Id, result2.ReportId);
        Assert.Equal(report.Id, result3.ReportId);
    }

    #endregion

    #region UpdateReportLinkAsync Tests

    [Fact(DisplayName = "Успешное обновление ссылки")]
    public async Task UpdateReportLinkAsync_WithNewData_ShouldUpdateLink()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var link = await CreateTestLinkAsync(report.Id, "https://original.com", "Original");
        var newDto = new ReportLinkDto { Link = "https://updated.com", Name = "Updated" };

        // Act
        var result = await _reportLinksDbClient.UpdateReportLinkInternalAsync(report.Id, link.Id, newDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(link.Id, result.Id);
        Assert.Equal(newDto.Link, result.Link);
        Assert.Equal(newDto.Name, result.Name);
        Assert.Equal(report.Id, result.ReportId);
        Assert.True(result.UpdatedAt > link.UpdatedAt);
        Assert.Equal(link.CreatedAt, result.CreatedAt); // CreatedAt не должен измениться
    }

    [Fact(DisplayName = "Обновление ссылки с organizationId")]
    public async Task UpdateReportLinkAsync_WithOrganizationId_ShouldUpdateLink()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var organizationId = $"org_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId, organizationId);
        var link = await CreateTestLinkAsync(report.Id, "https://original.com", "Original");
        var newDto = new ReportLinkDto { Link = "https://updated-org.com", Name = "Updated Org" };

        // Act
        var result = await _reportLinksDbClient.UpdateReportLinkInternalAsync(report.Id, link.Id, newDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(link.Id, result.Id);
        Assert.Equal(newDto.Link, result.Link);
        Assert.Equal(newDto.Name, result.Name);
    }

    [Fact(DisplayName = "Проверка что UpdatedAt обновляется при каждом изменении")]
    public async Task UpdateReportLinkAsync_ShouldUpdateTimestamp()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var link = await CreateTestLinkAsync(report.Id, "https://original.com", "Original");
        var initialUpdatedAt = link.UpdatedAt;

        // Небольшая задержка для гарантии различия времени
        await Task.Delay(10);

        // Act
        var newDto = new ReportLinkDto { Link = "https://updated.com", Name = "Updated" };
        var result = await _reportLinksDbClient.UpdateReportLinkInternalAsync(report.Id, link.Id, newDto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.UpdatedAt > initialUpdatedAt);
    }

    [Fact(DisplayName = "Обновление несуществующей ссылки должно вернуть null")]
    public async Task UpdateReportLinkAsync_NonExistentLink_ShouldReturnNull()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var nonExistentLinkId = 999999;
        var dto = new ReportLinkDto { Link = "https://example.com", Name = "Test" };

        // Act & Assert
        var result = await _reportLinksDbClient.UpdateReportLinkInternalAsync(report.Id, nonExistentLinkId, dto);
        Assert.Null(result);
    }

    #endregion

    #region DeleteReportLinkAsync Tests

    [Fact(DisplayName = "Успешное удаление ссылки")]
    public async Task DeleteReportLinkAsync_WithValidLink_ShouldDeleteLink()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var link = await CreateTestLinkAsync(report.Id, "https://to-delete.com", "To Delete");

        // Act
        await _reportLinksDbClient.DeleteReportLinkInternalAsync(report.Id, link.Id);

        // Assert - попытка обновить удаленную ссылку должна вернуть null
        var dto = new ReportLinkDto { Link = "https://should-not-update.com", Name = "Should not update" };
        var result = await _reportLinksDbClient.UpdateReportLinkInternalAsync(report.Id, link.Id, dto);
        Assert.Null(result);
    }


    [Fact(DisplayName = "Удаление одной из нескольких ссылок")]
    public async Task DeleteReportLinkAsync_OneOfMultiple_ShouldDeleteOnlyOne()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var link1 = await CreateTestLinkAsync(report.Id, "https://link1.com", "Link 1");
        var link2 = await CreateTestLinkAsync(report.Id, "https://link2.com", "Link 2");
        var link3 = await CreateTestLinkAsync(report.Id, "https://link3.com", "Link 3");

        // Act - удаляем вторую ссылку
        await _reportLinksDbClient.DeleteReportLinkInternalAsync(report.Id, link2.Id);

        // Assert - проверяем что link2 удалена
        var dto = new ReportLinkDto { Link = "https://should-not-update.com", Name = "Should not update" };
        var result = await _reportLinksDbClient.UpdateReportLinkInternalAsync(report.Id, link2.Id, dto);
        Assert.Null(result);

        // Assert - проверяем что link1 и link3 все еще существуют
        var updateDto1 = new ReportLinkDto { Link = "https://updated1.com", Name = "Updated 1" };
        var updateDto3 = new ReportLinkDto { Link = "https://updated3.com", Name = "Updated 3" };
        var updateResult1 = await _reportLinksDbClient.UpdateReportLinkInternalAsync(report.Id, link1.Id, updateDto1);
        var updateResult3 = await _reportLinksDbClient.UpdateReportLinkInternalAsync(report.Id, link3.Id, updateDto3);
        Assert.NotNull(updateResult1);
        Assert.NotNull(updateResult3);
        Assert.Equal(updateDto1.Link, updateResult1.Link);
        Assert.Equal(updateDto3.Link, updateResult3.Link);
    }

    [Fact(DisplayName = "Удаление несуществующей ссылки должно вернуть null")]
    public async Task DeleteReportLinkAsync_NonExistentLink_ShouldReturnNull()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);
        var nonExistentLinkId = 999999;

        // Act
        var result = await _reportLinksDbClient.DeleteReportLinkInternalAsync(report.Id, nonExistentLinkId);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Complex Workflow Tests

    [Fact(DisplayName = "Полный жизненный цикл ссылки: создание -> обновление -> удаление")]
    public async Task LinkLifecycle_CreateUpdateDelete_ShouldWorkCorrectly()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);

        // Act & Assert - Создание
        var createDto = new ReportLinkDto { Link = "https://initial.com", Name = "Initial" };
        var link = await _reportLinksDbClient.CreateReportLinkInternalAsync(report.Id, createDto);
        Assert.NotNull(link);
        Assert.Equal(createDto.Link, link.Link);
        Assert.Equal(createDto.Name, link.Name);

        // Act & Assert - Обновление
        var updateDto = new ReportLinkDto { Link = "https://updated.com", Name = "Updated" };
        var updated = await _reportLinksDbClient.UpdateReportLinkInternalAsync(report.Id, link.Id, updateDto);
        Assert.NotNull(updated);
        Assert.Equal(updateDto.Link, updated.Link);
        Assert.Equal(updateDto.Name, updated.Name);
        Assert.Equal(link.Id, updated.Id);

        // Act & Assert - Удаление
        await _reportLinksDbClient.DeleteReportLinkInternalAsync(report.Id, link.Id);

        // Assert - после удаления попытка обновления должна вернуть null
        var failDto = new ReportLinkDto { Link = "https://should-fail.com", Name = "Should fail" };
        var result = await _reportLinksDbClient.UpdateReportLinkInternalAsync(report.Id, link.Id, failDto);
        Assert.Null(result);
    }

    [Fact(DisplayName = "Создание ссылок к разным репортам")]
    public async Task CreateReportLinkAsync_DifferentReports_ShouldCreateCorrectly()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report1 = await CreateTestReportAsync(userId);
        var report2 = await CreateTestReportAsync(userId);
        var dto1 = new ReportLinkDto { Link = "https://report1-link.com", Name = "Report 1 Link" };
        var dto2 = new ReportLinkDto { Link = "https://report2-link.com", Name = "Report 2 Link" };

        // Act
        var link1 = await _reportLinksDbClient.CreateReportLinkInternalAsync(report1.Id, dto1);
        var link2 = await _reportLinksDbClient.CreateReportLinkInternalAsync(report2.Id, dto2);

        // Assert
        Assert.NotNull(link1);
        Assert.NotNull(link2);
        Assert.NotEqual(link1.Id, link2.Id);
        Assert.Equal(report1.Id, link1.ReportId);
        Assert.Equal(report2.Id, link2.ReportId);
        Assert.Equal(dto1.Link, link1.Link);
        Assert.Equal(dto2.Link, link2.Link);
    }

    #endregion

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

    private async Task<Bugget.Entities.DbModels.ReportLink.ReportLinkDbModel> CreateTestLinkAsync(
        int reportId,
        string link,
        string name)
    {
        var dto = new ReportLinkDto { Link = link, Name = name };
        return await _reportLinksDbClient.CreateReportLinkInternalAsync(reportId, dto);
    }

    #endregion
}

