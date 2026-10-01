using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>Контракт SignalR-хаба: проверяется HTTP-negotiate — без него страница репорта не обновляется вживую.</summary>
[Collection("PostgresCollection")]
public sealed class ReportPageHubContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    [Fact(DisplayName = "POST /v1/report-page-hub/negotiate: 200 и connectionId")]
    public async Task Negotiate()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PostAsync("/v1/report-page-hub/negotiate?negotiateVersion=1", null);

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("connectionId").GetString()));
        Assert.NotEmpty(body.GetProperty("availableTransports").EnumerateArray().ToArray());
    }

    // Хаб не закрыт [Authorize]: защита только auth_request в nginx — тест фиксирует эту зависимость.
    [Fact(DisplayName = "POST /v1/report-page-hub/negotiate без identity: 200, защита только на nginx")]
    public async Task NegotiateWithoutIdentity()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsync("/v1/report-page-hub/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Снимается настоящий completion error живого соединения: рамку вокруг сообщения добавляет
    // сам SignalR. Форма записана в specs/contracts/events.yaml (methodError).
    [Fact(DisplayName = "Отказ метода хаба: payload {code, title} из общего каталога внутри рамки SignalR")]
    public async Task MethodFailureCarriesTheCatalogEnvelope()
    {
        var scenario = ContractScenario.Create(fixture);
        await using var connection = BuildConnection(scenario);
        await connection.StartAsync();

        var exception = await Assert.ThrowsAsync<HubException>(
            () => connection.InvokeAsync("JoinReportGroupAsync", "404404404"));

        // Источник истины по рамке — methodError.envelope в events.yaml: гейт realtime-contract
        // требует, чтобы префикс из контракта встречался здесь дословно.
        Assert.StartsWith(
            "An unexpected error occurred invoking 'JoinReportGroupAsync' on the server. RealtimeProblemException: ",
            exception.Message,
            StringComparison.Ordinal);

        using var document = JsonDocument.Parse(PayloadOf(exception.Message));
        var root = document.RootElement;

        Assert.Equal("report_not_found", root.GetProperty("code").GetString());
        Assert.Equal("Репорт не найден", root.GetProperty("title").GetString());
        Assert.Equal(2, root.EnumerateObject().Count());
    }

    // Detailed errors отдают клиенту текст любого необработанного исключения в обход фильтра границы.
    [Fact(DisplayName = "У хаба выключены detailed errors")]
    public void DetailedErrorsAreOff()
    {
        var options = fixture.Services.GetRequiredService<IOptions<HubOptions>>().Value;

        Assert.False(options.EnableDetailedErrors);
    }

    private static string PayloadOf(string message) =>
        message[(message.IndexOf(": ", StringComparison.Ordinal) + 2)..];

    private HubConnection BuildConnection(ContractScenario scenario) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(fixture.BaseAddress, "v1/report-page-hub"), options =>
            {
                // У TestServer нет настоящего сокета — long polling через in-memory handler.
                options.HttpMessageHandlerFactory = _ => fixture.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers.Add(ContractHeaders.UserId, scenario.UserId);
                options.Headers.Add(ContractHeaders.TeamId, scenario.TeamId);
                options.Headers.Add(ContractHeaders.WorkspaceId, scenario.WorkspaceId);
                options.Headers.Add(ContractHeaders.WorkspaceRole, "owner");
            })
            .Build();
}
