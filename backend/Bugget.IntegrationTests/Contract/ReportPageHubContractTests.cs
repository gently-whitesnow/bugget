using System.Net;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контракт SignalR-хаба страницы репорта. Фронт открывает его по
/// <c>/api/app/workspaces/{id}/teams/{id}/v1/report-page-hub</c>, а до самого сокета
/// делает HTTP-negotiate — именно он и проверяется: пропадёт путь или сменится форма
/// ответа negotiate, и страница репорта перестанет обновляться вживую.
/// </summary>
[Collection("PostgresCollection")]
public sealed class ReportPageHubContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    [Fact(DisplayName = "POST /v1/report-page-hub/negotiate: 200 и connectionId")]
    public async Task Negotiate()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PostAsync("/v1/report-page-hub/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v1.report-page-hub.negotiate.post", response);
    }

    /// <summary>
    /// Хаб не закрыт [Authorize]: negotiate отвечает 200 и без identity-заголовков.
    /// В бою до него доходят только запросы, прошедшие auth_request в nginx, —
    /// снимок фиксирует эту зависимость, чтобы её нельзя было потерять молча.
    /// </summary>
    [Fact(DisplayName = "POST /v1/report-page-hub/negotiate без identity: 200, защита только на nginx")]
    public async Task NegotiateWithoutIdentity()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsync("/v1/report-page-hub/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
