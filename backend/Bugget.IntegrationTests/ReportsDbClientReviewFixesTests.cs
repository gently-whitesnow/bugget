using Bugget.DA.Interfaces;
using Bugget.Entities.DTO.Report;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public sealed class ReportsDbClientReviewFixesTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IReportsDbClient _reportsDbClient;

    public ReportsDbClientReviewFixesTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _reportsDbClient = scope.ServiceProvider.GetRequiredService<IReportsDbClient>();
    }

    [Fact(DisplayName = "GET репорта возвращает флаг исключения из аналитики")]
    public async Task GetReportAsync_ExcludedFromAnalytics_ShouldReturnFlag()
    {
        var created = await CreateTestReportAsync();
        await _reportsDbClient.PatchReportAsync(
            created.Id,
            new ReportPatchDto { IsExcludedFromAnalytics = true });

        var result = await _reportsDbClient.GetReportInternalAsync(created.Id);

        Assert.NotNull(result);
        Assert.True(result.IsExcludedFromAnalytics);
    }

    [Fact(DisplayName = "Повторный PATCH с тем же responsible не меняет past_responsible_user_id")]
    public async Task PatchReportAsync_SameResponsible_ShouldKeepPastResponsible()
    {
        var userId = $"user_{Guid.NewGuid()}";
        var newResponsibleUserId = $"user_{Guid.NewGuid()}";
        var report = await CreateTestReportAsync(userId);

        var firstPatch = await _reportsDbClient.PatchReportAsync(
            report.Id,
            new ReportPatchDto { ResponsibleUserId = newResponsibleUserId });
        var secondPatch = await _reportsDbClient.PatchReportAsync(
            report.Id,
            new ReportPatchDto { ResponsibleUserId = newResponsibleUserId });

        Assert.Equal(newResponsibleUserId, firstPatch.ResponsibleUserId);
        Assert.Equal(userId, firstPatch.PastResponsibleUserId);
        Assert.Equal(newResponsibleUserId, secondPatch.ResponsibleUserId);
        Assert.Equal(userId, secondPatch.PastResponsibleUserId);
    }

    private Task<Bugget.Entities.DbModels.Report.ReportSummaryDbModel> CreateTestReportAsync(
        string? userId = null)
    {
        return _reportsDbClient.CreateReportAsync(
            userId ?? $"user_{Guid.NewGuid()}",
            teamId: null,
            organizationId: null,
            new ReportCreateDto { Title = $"Test Report {Guid.NewGuid()}" });
    }
}
