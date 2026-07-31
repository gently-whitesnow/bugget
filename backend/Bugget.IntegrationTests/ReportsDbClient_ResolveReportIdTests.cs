using Bugget.Application.Ports;
using Bugget.Contracts.Dto.Report;
using Bugget.Domain.Reports;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class ReportsDbClient_ResolveReportIdTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IReportsDbClient _reportsDbClient;

    public ReportsDbClient_ResolveReportIdTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _reportsDbClient = scope.ServiceProvider.GetRequiredService<IReportsDbClient>();
    }

    public enum ResolveBy
    {
        ReportId,
        PublicId
    }

    [Theory(DisplayName = "Разрешение ID отчета без фильтров (reportId/public_id)")]
    [InlineData(ResolveBy.ReportId)]
    [InlineData(ResolveBy.PublicId)]
    public async Task ResolveReportIdAsync_WithoutFilters_ShouldReturnCorrectId(ResolveBy resolveBy)
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);

        // Act
        var result = await ResolveByAsync(resolveBy, workspaceId: null, report);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(report.Id, result.Id);
    }

    [Theory(DisplayName = "Разрешение ID отчета с workspaceId фильтром (совпадение/несовпадение)")]
    [InlineData(ResolveBy.ReportId, true, true)]
    [InlineData(ResolveBy.ReportId, false, false)]
    [InlineData(ResolveBy.PublicId, true, true)]
    [InlineData(ResolveBy.PublicId, false, false)]
    public async Task ResolveReportIdAsync_WithWorkspaceIdFilter_ShouldBehaveCorrectly(
        ResolveBy resolveBy,
        bool workspaceIdMatches,
        bool shouldResolve)
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var workspaceId = $"workspace_{Guid.NewGuid()}";
        var wrongWorkspaceId = $"wrong_workspace_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId, organizationId: workspaceId);

        var filterWorkspaceId = workspaceIdMatches ? workspaceId : wrongWorkspaceId;

        // Act
        var result = await ResolveByAsync(resolveBy, workspaceId: filterWorkspaceId, report);

        // Assert
        if (shouldResolve)
        {
            Assert.NotNull(result);
            Assert.Equal(report.Id, result.Id);
        }
        else
        {
            Assert.Null(result);
        }
    }

    [Fact(DisplayName = "Разрешение ID отчета по несуществующему reportId")]
    public async Task ResolveReportIdAsync_ByNonExistentReportId_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = 999999;

        // Act
        var result = await _reportsDbClient.ResolveReportIdAsync(null, null, nonExistentId, null, null);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Разрешение ID отчета по несуществующему public_id")]
    public async Task ResolveReportIdAsync_ByNonExistentPublicId_ShouldReturnNull()
    {
        // Arrange
        var nonExistentPublicId = Guid.NewGuid();

        // Act
        var result = await _reportsDbClient.ResolveReportIdAsync(null, null, null, nonExistentPublicId, null);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Разрешение ID отчета без параметров - null")]
    public async Task ResolveReportIdAsync_WithAllNullParameters_ShouldReturnNull()
    {
        // Act
        var result = await _reportsDbClient.ResolveReportIdAsync(null, null, null, null, null);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Разрешение ID отчета по reportId имеет приоритет над public_id")]
    public async Task ResolveReportIdAsync_ReportIdHasPriorityOverPublicId_ShouldUseReportId()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var report1 = await CreateTestReportAsync(userId);
        var report2 = await CreateTestReportAsync(userId);

        // Act - передаем reportId от первого отчета и public_id от второго
        var result = await _reportsDbClient.ResolveReportIdAsync(null, null, report1.Id, report2.PublicId, null);

        // Assert - должен вернуться ID первого отчета (приоритет reportId)
        Assert.NotNull(result);
        Assert.Equal(report1.Id, result.Id);
    }

    [Fact(DisplayName = "Разрешение team_report_id учитывает teamId и не путает репорты разных команд")]
    public async Task ResolveReportIdAsync_ByTeamReportId_WithTeamFilter_ShouldResolveCorrectTeamReport()
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var workspaceId = $"workspace_{Guid.NewGuid()}";
        var teamId1 = $"team_{Guid.NewGuid()}";
        var teamId2 = $"team_{Guid.NewGuid()}";

        var teamReport1 = await CreateTestReportAsync(userId, teamId: teamId1, organizationId: workspaceId);
        var teamReport2 = await CreateTestReportAsync(userId, teamId: teamId2, organizationId: workspaceId);

        Assert.NotNull(teamReport1.TeamReportId);
        Assert.NotNull(teamReport2.TeamReportId);
        Assert.Equal(teamReport1.TeamReportId, teamReport2.TeamReportId);

        // Act
        var team1Result = await _reportsDbClient.ResolveReportIdAsync(
            workspaceId,
            teamId1,
            reportId: null,
            publicId: null,
            teamReportId: teamReport1.TeamReportId);
        var team2Result = await _reportsDbClient.ResolveReportIdAsync(
            workspaceId,
            teamId2,
            reportId: null,
            publicId: null,
            teamReportId: teamReport2.TeamReportId);
        var wrongTeamResult = await _reportsDbClient.ResolveReportIdAsync(
            workspaceId,
            $"unknown_team_{Guid.NewGuid()}",
            reportId: null,
            publicId: null,
            teamReportId: teamReport1.TeamReportId);

        // Assert
        Assert.NotNull(team1Result);
        Assert.Equal(teamReport1.Id, team1Result.Id);

        Assert.NotNull(team2Result);
        Assert.Equal(teamReport2.Id, team2Result.Id);

        Assert.Null(wrongTeamResult);
    }

    [Theory(DisplayName = "Workspace-scoped report по reportId/public_id доступен из любого team-контекста того же workspace")]
    [InlineData(ResolveBy.ReportId)]
    [InlineData(ResolveBy.PublicId)]
    public async Task ResolveReportIdAsync_WorkspaceScopedReport_WithTeamFilterInSameWorkspace_ShouldResolve(
        ResolveBy resolveBy)
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var workspaceId = $"workspace_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId, teamId: null, organizationId: workspaceId);

        // Act
        var result = await _reportsDbClient.ResolveReportIdAsync(
            workspaceId,
            $"team_{Guid.NewGuid()}",
            reportId: resolveBy == ResolveBy.ReportId ? report.Id : null,
            publicId: resolveBy == ResolveBy.PublicId ? report.PublicId : null,
            teamReportId: null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(report.Id, result.Id);
        Assert.Null(result.CreatorTeamId);
    }

    [Theory(DisplayName = "Team-scoped report по reportId/public_id не резолвится через чужой team-контекст")]
    [InlineData(ResolveBy.ReportId)]
    [InlineData(ResolveBy.PublicId)]
    public async Task ResolveReportIdAsync_TeamScopedReport_WithWrongTeamFilter_ShouldReturnNull(
        ResolveBy resolveBy)
    {
        // Arrange
        var userId = $"user_{Guid.NewGuid()}";
        var workspaceId = $"workspace_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(
            userId,
            teamId: $"team_{Guid.NewGuid()}",
            organizationId: workspaceId);

        // Act
        var result = await _reportsDbClient.ResolveReportIdAsync(
            workspaceId,
            $"team_{Guid.NewGuid()}",
            reportId: resolveBy == ResolveBy.ReportId ? report.Id : null,
            publicId: resolveBy == ResolveBy.PublicId ? report.PublicId : null,
            teamReportId: null);

        // Assert
        Assert.Null(result);
    }

    private async Task<Bugget.Domain.Reports.ResolvedReportId?> ResolveByAsync(
        ResolveBy resolveBy,
        string? workspaceId,
        ReportSummary report)
    {
        return await _reportsDbClient.ResolveReportIdAsync(
            workspaceId,
            teamId: null,
            reportId: resolveBy == ResolveBy.ReportId ? report.Id : null,
            publicId: resolveBy == ResolveBy.PublicId ? report.PublicId : null,
            teamReportId: null);
    }

    private async Task<ReportSummary> CreateTestReportAsync(
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
}
