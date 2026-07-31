using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

        // Три группы — рабочее пространство, команда, пользователь: фронт рисует по
        // ним три раздела и не проверяет ключи на существование. Пользовательских
        // процессоров в OSS-сборке нет, поэтому группа пустая, но присутствует.
        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.NotEmpty(body.GetProperty("workspace_sections").EnumerateArray().ToArray());
        Assert.NotEmpty(body.GetProperty("team_sections").EnumerateArray().ToArray());
        Assert.Empty(body.GetProperty("user_sections").EnumerateArray().ToArray());
    }

    [Fact(DisplayName = "PUT /v1/workspace-settings-sections/{sectionId}/settings/{settingId}: 200")]
    public async Task UpdateWorkspaceSetting()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PutAsJsonAsync(
            $"/v1/workspace-settings-sections/{KaitenSection}/settings/domain",
            new[] { "example.kaiten.ru" });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal("domain", body.GetProperty("id").GetString());
        Assert.Equal(
            new[] { "example.kaiten.ru" },
            body.GetProperty("values").EnumerateArray().Select(value => value.GetString()).ToArray());
    }

    [Fact(DisplayName = "PUT настройки workspace с неизвестной секцией: ошибка, а не 200")]
    public async Task UpdateUnknownWorkspaceSetting()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PutAsJsonAsync(
            "/v1/workspace-settings-sections/unknown/settings/domain",
            new[] { "value" });

        await ContractResponse.ProblemAsync(
            response,
            "workspace_settings_section_not_found",
            HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "PUT /v1/team-settings-sections/{sectionId}/settings/{settingId}: 200")]
    public async Task UpdateTeamSetting()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PutAsJsonAsync(
            $"/v1/team-settings-sections/{KaitenSection}/settings/use_report_linking",
            new[] { "true" });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal("use_report_linking", body.GetProperty("id").GetString());

        // Булеву настройку сервер отдаёт своим представлением ("True"), а не тем,
        // что прислал клиент, — фронт разбирает ответ без учёта регистра.
        Assert.Equal(
            new[] { "True" },
            body.GetProperty("values").EnumerateArray().Select(value => value.GetString()).ToArray());
    }

    /// <summary>
    /// Пользовательских процессоров настроек в OSS-сборке не зарегистрировано, поэтому
    /// путь существует, но всегда отвечает 404 из общего каталога.
    /// </summary>
    [Fact(DisplayName = "PUT /v1/user-settings-sections/{sectionId}/settings/{settingId}: секции нет")]
    public async Task UpdateUserSetting()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PutAsJsonAsync(
            $"/v1/user-settings-sections/{KaitenSection}/settings/anything",
            new[] { "true" });

        await ContractResponse.ProblemAsync(
            response,
            "user_settings_section_not_found",
            HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GET /v1/reports/search: 200, total + reports")]
    public async Task SearchReports()
    {
        var scenario = ContractScenario.Create(fixture);
        await scenario.CreateReportAsync("ищем именно этот репорт");

        var response = await scenario.Client.GetAsync("/v1/reports/search?query=репорт&skip=0&take=10");

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(1, body.GetProperty("total").GetInt32());
        var report = Assert.Single(body.GetProperty("reports").EnumerateArray().ToArray());
        Assert.Equal("ищем именно этот репорт", report.GetProperty("title").GetString());
    }

    /// <summary>
    /// Поиск — второй адрес той же формы списка (<c>ReportList</c>), и сужение
    /// элемента списка обязано действовать и здесь. Проверяется на полном сиде и
    /// поэлементно: ссылки, вложения бага и шаги лежат в базе, но в ответе поиска
    /// их быть не должно, а комментарии — должны.
    /// </summary>
    [Fact(DisplayName = "GET /v1/reports/search: в элементе результата нет links, вложений бага и шагов")]
    public async Task SearchReportsOmitsKeysItDoesNotLoad()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync("ищем именно этот репорт с багом");
        var bugId = await scenario.CreateBugAsync(reportId);
        var commentId = await scenario.CreateCommentAsync(reportId, bugId);
        var stepId = await scenario.CreateStepAsync(reportId, bugId);
        await scenario.UploadBugAttachmentAsync(reportId, bugId);
        await scenario.UploadBugStepAttachmentAsync(reportId, bugId, stepId);
        await scenario.CreateLinkAsync(reportId);

        var response = await scenario.Client.GetAsync("/v1/reports/search?query=репорт&skip=0&take=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ContractScenario.ReadJsonAsync(response);
        var report = Assert.Single(
            body.GetProperty("reports").EnumerateArray().ToArray(),
            item => item.GetProperty("id").GetString() == reportId);

        Assert.False(report.TryGetProperty("links", out _), "поиск отдал links, которых не загружает");

        var bug = Assert.Single(
            report.GetProperty("bugs").EnumerateArray().ToArray(),
            item => item.GetProperty("id").GetInt32() == bugId);
        Assert.False(bug.TryGetProperty("attachments", out _), "поиск отдал вложения бага, которых не загружает");
        Assert.False(bug.TryGetProperty("steps", out _), "поиск отдал шаги, которых не загружает");

        var comment = Assert.Single(bug.GetProperty("comments").EnumerateArray().ToArray());
        Assert.Equal(commentId, comment.GetProperty("id").GetInt32());
    }

    [Fact(DisplayName = "GET /v1/external/search: 200, total + items")]
    public async Task ExternalSearch()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync("/v1/external/search?query=abc&skip=0&take=10");

        // Внешние источники в тестовом контуре не настроены: путь живой и отдаёт
        // пустую выдачу, а не ошибку, — фронт рисует по ней «ничего не найдено».
        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(0, body.GetProperty("total").GetInt32());
        Assert.Empty(body.GetProperty("items").EnumerateArray().ToArray());
    }

    [Fact(DisplayName = "POST /v1/external/search/apply: без настроенного источника — 200 и пустое тело")]
    public async Task ApplyExternalSearch()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        var response = await scenario.Client.PostAsJsonAsync(
            "/v1/external/search/apply",
            new { id = "1", source = "kaiten", report_id = reportId });

        await ContractResponse.EmptyAsync(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GET /v1/external/kaiten/boards: 200, массив досок")]
    public async Task KaitenBoards()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync("/v1/external/kaiten/boards?skip=0&take=10");

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    [Fact(DisplayName = "POST /v1/external/kaiten/boards/batch-get: 200, массив досок")]
    public async Task KaitenBoardsBatchGet()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PostAsJsonAsync(
            "/v1/external/kaiten/boards/batch-get",
            new { ids = new[] { 1L } });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }
}
