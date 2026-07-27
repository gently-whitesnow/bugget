using System.Net;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контракт аналитики. Поведение (что попадает в выборку) проверяется в
/// <see cref="AnalyticsControllerTests"/>; здесь снимается только форма ответа —
/// её ломает любая правка сгенерированных из OpenAPI контрактов
/// (<c>Bugget.Analytics.Contracts</c>, <c>Bugget.Reports.Contracts</c>).
/// </summary>
[Collection("PostgresCollection")]
public sealed class AnalyticsContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    [Fact(DisplayName = "GET /v2/analytics/summary: 200 и форма AnalyticsSummary")]
    public async Task Summary()
    {
        var scenario = ContractScenario.Create(fixture);
        await scenario.CreateReportAsync();

        var response = await scenario.Client.GetAsync("/v2/analytics/summary?period=30d");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.analytics.summary.get", response);
    }

    [Fact(DisplayName = "GET /v2/analytics/responsible/{userId}: 200 и форма AnalyticsResponsible")]
    public async Task Responsible()
    {
        var scenario = ContractScenario.Create(fixture);
        await scenario.CreateReportAsync();

        var response = await scenario.Client.GetAsync(
            $"/v2/analytics/responsible/{scenario.UserId}?period=30d");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.analytics.responsible.get", response);
    }

    [Fact(DisplayName = "GET /v2/reports/{id}/analytics: 200 и форма AnalyticsReport")]
    public async Task ReportAnalytics()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        var response = await scenario.Client.GetAsync($"/v2/reports/{reportId}/analytics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.reports.analytics.get", response);
    }

    [Fact(DisplayName = "GET /v2/reports/{id}/analytics: нечисловой сегмент не совпадает с маршрутом — 404")]
    public async Task ReportAnalyticsWithNonNumericId()
    {
        // Ограничение маршрута (:long) держит поведение «мусорный сегмент — это не наш
        // путь». Без него запрос доехал бы до действия и вернул 400 на связывании.
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync("/v2/reports/not-a-number/analytics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
