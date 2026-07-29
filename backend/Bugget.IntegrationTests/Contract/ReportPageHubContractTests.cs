using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

    /// <summary>
    /// Отказ метода хаба — такой же публичный провод, как событие, и до MAIN-69 он не был
    /// снят ничем. Снимается настоящий completion error живого соединения, а не то, что
    /// хаб бросает: рамку вокруг сообщения добавляет сам SignalR, и знать о ней контракт
    /// обязан. Форма записана в <c>specs/contracts/events.yaml</c> (<c>methodError</c>).
    /// </summary>
    [Fact(DisplayName = "Отказ метода хаба: payload {code, title} из общего каталога внутри рамки SignalR")]
    public async Task MethodFailureCarriesTheCatalogEnvelope()
    {
        var scenario = ContractScenario.Create(fixture);
        await using var connection = BuildConnection(scenario);
        await connection.StartAsync();

        var exception = await Assert.ThrowsAsync<HubException>(
            () => connection.InvokeAsync("JoinReportGroupAsync", "404404404"));

        Assert.StartsWith(
            "An unexpected error occurred invoking 'JoinReportGroupAsync' on the server. RealtimeProblemException: ",
            exception.Message,
            StringComparison.Ordinal);

        using var document = JsonDocument.Parse(PayloadOf(exception.Message));
        var root = document.RootElement;

        Assert.Equal("report_not_found", root.GetProperty("code").GetString());
        Assert.Equal("Репорт не найден", root.GetProperty("title").GetString());
        // Ровно два поля: RFC-механики HTTP в сокете нет и быть не должно.
        Assert.Equal(2, root.EnumerateObject().Count());
    }

    /// <summary>
    /// Detailed errors отдают клиенту текст любого необработанного исключения в обход
    /// фильтра границы. Проверяется настройка работающего хоста, а не строчка в коде.
    /// </summary>
    [Fact(DisplayName = "У хаба выключены detailed errors")]
    public void DetailedErrorsAreOff()
    {
        // Настройка глобальная: её задаёт AddSignalR, а не AddHubOptions<T>.
        var options = fixture.Services.GetRequiredService<IOptions<HubOptions>>().Value;

        Assert.False(options.EnableDetailedErrors);
    }

    /// <summary>Наш слой — хвост после рамки, которую навесил SignalR.</summary>
    private static string PayloadOf(string message) =>
        message[(message.IndexOf(": ", StringComparison.Ordinal) + 2)..];

    private HubConnection BuildConnection(ContractScenario scenario) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(fixture.BaseAddress, "v1/report-page-hub"), options =>
            {
                // У TestServer нет настоящего сокета: клиент ходит long polling'ом
                // через его in-memory handler. Форма completion error от транспорта
                // не зависит.
                options.HttpMessageHandlerFactory = _ => fixture.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers.Add(ContractHeaders.UserId, scenario.UserId);
                options.Headers.Add(ContractHeaders.TeamId, scenario.TeamId);
                options.Headers.Add(ContractHeaders.WorkspaceId, scenario.WorkspaceId);
                options.Headers.Add(ContractHeaders.WorkspaceRole, "owner");
            })
            .Build();
}
