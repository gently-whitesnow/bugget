using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контракт настроек (<c>/v1/*-settings-sections/**</c>), поиска по репортам
/// (<c>/v1/reports/search</c>) и внешних источников (<c>/v1/external/**</c>).
/// Пути живут в v1 и остаются в v1: фронт ходит именно по ним.
/// </summary>
[Collection("PostgresCollection")]
public sealed class SettingsAndSearchContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    private const string KaitenSection = "kaiten";

    [Fact(DisplayName = "GET /v1/settings-sections: 200, три группы секций")]
    public async Task GetSettingsSections()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync("/v1/settings-sections");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v1.settings-sections.get", response);
    }

    [Fact(DisplayName = "PUT /v1/workspace-settings-sections/{sectionId}/settings/{settingId}: 200")]
    public async Task UpdateWorkspaceSetting()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PutAsJsonAsync(
            $"/v1/workspace-settings-sections/{KaitenSection}/settings/domain",
            new[] { "example.kaiten.ru" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v1.workspace-settings.put", response);
    }

    [Fact(DisplayName = "PUT настройки workspace с неизвестной секцией: ошибка, а не 200")]
    public async Task UpdateUnknownWorkspaceSetting()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PutAsJsonAsync(
            "/v1/workspace-settings-sections/unknown/settings/domain",
            new[] { "value" });

        await ContractSnapshot.MatchAsync("v1.workspace-settings.put.unknown-section", response);
    }

    [Fact(DisplayName = "PUT /v1/team-settings-sections/{sectionId}/settings/{settingId}: 200")]
    public async Task UpdateTeamSetting()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PutAsJsonAsync(
            $"/v1/team-settings-sections/{KaitenSection}/settings/use_report_linking",
            new[] { "true" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v1.team-settings.put", response);
    }

    /// <summary>
    /// Пользовательских процессоров настроек в OSS-сборке не зарегистрировано, поэтому
    /// путь существует, но всегда отвечает ошибкой. Снимок фиксирует именно это.
    /// </summary>
    [Fact(DisplayName = "PUT /v1/user-settings-sections/{sectionId}/settings/{settingId}: секции нет")]
    public async Task UpdateUserSetting()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PutAsJsonAsync(
            $"/v1/user-settings-sections/{KaitenSection}/settings/anything",
            new[] { "true" });

        await ContractSnapshot.MatchAsync("v1.user-settings.put", response);
    }

    [Fact(DisplayName = "GET /v1/reports/search: 200, total + reports")]
    public async Task SearchReports()
    {
        var scenario = ContractScenario.Create(fixture);
        await scenario.CreateReportAsync("ищем именно этот репорт");

        var response = await scenario.Client.GetAsync("/v1/reports/search?query=репорт&skip=0&take=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v1.reports.search.get", response);
    }

    [Fact(DisplayName = "GET /v1/external/search: 200, total + items")]
    public async Task ExternalSearch()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync("/v1/external/search?query=abc&skip=0&take=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v1.external.search.get", response);
    }

    [Fact(DisplayName = "POST /v1/external/search/apply: несуществующий источник — ошибка")]
    public async Task ApplyExternalSearch()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        var response = await scenario.Client.PostAsJsonAsync(
            "/v1/external/search/apply",
            new { id = "1", source = "kaiten", report_id = reportId });

        await ContractSnapshot.MatchAsync("v1.external.search-apply.post", response);
    }

    [Fact(DisplayName = "GET /v1/external/kaiten/boards: 200, массив досок")]
    public async Task KaitenBoards()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync("/v1/external/kaiten/boards?skip=0&take=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v1.external.kaiten-boards.get", response);
    }

    [Fact(DisplayName = "POST /v1/external/kaiten/boards/batch-get: 200, массив досок")]
    public async Task KaitenBoardsBatchGet()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PostAsJsonAsync(
            "/v1/external/kaiten/boards/batch-get",
            new { ids = new[] { 1L } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v1.external.kaiten-boards-batch-get.post", response);
    }
}
