using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контракт репортов: <c>/v2/reports</c> и его под-ресурсы. Это то, с чего начинается
/// любая страница фронта, поэтому здесь снимается и форма ответа, и коды ошибок.
/// </summary>
[Collection("PostgresCollection")]
public sealed class ReportsContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    /// <summary>
    /// Статус 200, а не 201: контроллер объявляет ProducesResponseType(201), но возвращает
    /// модель напрямую. Снимок фиксирует то, что реально уходит фронту.
    /// </summary>
    [Fact(DisplayName = "POST /v2/reports: 200 и форма ReportSummary")]
    public async Task CreateReport()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PostAsJsonAsync("/v2/reports", new { title = "contract-report" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.reports.post", response);
    }

    [Fact(DisplayName = "POST /v2/reports без title: 400 model_state_validation_error")]
    public async Task CreateReportWithoutTitle()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PostAsJsonAsync("/v2/reports", new { title = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.reports.post.invalid", response);
    }

    /// <summary>
    /// Сид намеренно полный: пустая коллекция в ответе не предъявляет формы своего
    /// элемента, а `null` в ключе не отличим от отсутствия ключа. Поэтому здесь есть
    /// и вложение, и ссылка, и баги, у которых заполнено ровно одно поле из пары
    /// `receive`/`expect` — снимок доказывает, что оба ключа присутствуют всегда,
    /// а `null` в них законен (см. `required` + `nullable` в контракте).
    /// </summary>
    [Fact(DisplayName = "GET /v2/reports/{aliasId}: 200 и форма Report с вложенными сущностями")]
    public async Task GetReport()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        await scenario.CreateCommentAsync(reportId, bugId);
        await scenario.CreateStepAsync(reportId, bugId);
        await scenario.UploadBugAttachmentAsync(reportId, bugId);
        await scenario.CreateLinkAsync(reportId);
        await scenario.CreateOneFieldBugAsync(reportId, receive: "только факт");
        await scenario.CreateOneFieldBugAsync(reportId, expect: "только ожидание");

        var response = await scenario.Client.GetAsync($"/v2/reports/{reportId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.reports.get", response);
    }

    [Fact(DisplayName = "GET /v2/reports/{aliasId}: чужой репорт не отдаётся")]
    public async Task GetForeignReport()
    {
        var owner = ContractScenario.Create(fixture);
        var stranger = ContractScenario.Create(fixture);
        var reportId = await owner.CreateReportAsync();

        var response = await stranger.Client.GetAsync($"/v2/reports/{reportId}");

        await ContractSnapshot.MatchAsync("v2.reports.get.foreign", response);
    }

    [Fact(DisplayName = "PATCH /v2/reports/{aliasId}: 200 и форма ReportPatchResult")]
    public async Task PatchReport()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        var response = await scenario.Client.PatchAsJsonAsync(
            $"/v2/reports/{reportId}",
            new { title = "переименовали", is_excluded_from_analytics = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.reports.patch", response);
    }

    [Fact(DisplayName = "GET /v2/reports: 200, total + reports")]
    public async Task ListReports()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        await scenario.CreateBugAsync(reportId);

        var response = await scenario.Client.GetAsync("/v2/reports?skip=0&take=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.reports.list", response);
    }

    [Fact(DisplayName = "GET /v2/reports с take вне диапазона: 400")]
    public async Task ListReportsWithInvalidTake()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync("/v2/reports?skip=0&take=1000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.reports.list.invalid", response);
    }

    [Fact(DisplayName = "GET /v2/reports/legacy/{legacyId}: teamId + teamReportId для редиректа")]
    public async Task ResolveLegacyReport()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        var response = await scenario.Client.GetAsync($"/v2/reports/legacy/{reportId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.reports.legacy.get", response);
    }

    [Fact(DisplayName = "GET /v2/reports/legacy/{legacyId}: несуществующий — 404")]
    public async Task ResolveMissingLegacyReport()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync("/v2/reports/legacy/2147483000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "GET /v2/reports/legacy/{legacyId}: нечисловой сегмент не совпадает с маршрутом — 404")]
    public async Task ResolveLegacyReportWithNonNumericId()
    {
        // Ограничение маршрута (:int) держит поведение «мусорный сегмент — это не наш
        // путь». Без него запрос доехал бы до действия и вернул 400 на связывании.
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync("/v2/reports/legacy/not-a-number");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "POST /v2/reports/counts:batch: 200, counts по ключам запроса")]
    public async Task CountsBatch()
    {
        var scenario = ContractScenario.Create(fixture);
        await scenario.CreateReportAsync();

        var response = await scenario.Client.PostAsJsonAsync(
            "/v2/reports/counts:batch",
            new { scopes = new[] { new { key = "all" } } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.reports.counts-batch.post", response);
    }

    [Fact(DisplayName = "POST /v2/reports/counts:batch с дублем ключа: 400 duplicate_scope_key")]
    public async Task CountsBatchWithDuplicateKey()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PostAsJsonAsync(
            "/v2/reports/counts:batch",
            new { scopes = new[] { new { key = "all" }, new { key = "all" } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.reports.counts-batch.post.duplicate", response);
    }
}
