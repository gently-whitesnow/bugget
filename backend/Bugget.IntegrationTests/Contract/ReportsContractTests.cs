using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контракт репортов: <c>/v2/reports</c> и его под-ресурсы. Это то, с чего начинается
/// любая страница фронта, поэтому здесь проверяются и статусы, и коды ошибок, и
/// поведенческие инварианты ответа.
/// </summary>
[Collection("PostgresCollection")]
public sealed class ReportsContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    [Fact(DisplayName = "POST /v2/reports: 201, Location и созданный ReportSummary")]
    public async Task CreateReport()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PostAsJsonAsync("/v2/reports", new { title = "contract-report" });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.Created);
        Assert.Equal("contract-report", body.GetProperty("title").GetString());
        var aliasId = body.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(aliasId));
        Assert.Equal(scenario.UserId, body.GetProperty("creator_user_id").GetString());
        Assert.Equal(scenario.TeamId, body.GetProperty("creator_team_id").GetString());
        Assert.Equal(
            $"/api/app/workspaces/{scenario.WorkspaceId}/teams/{scenario.TeamId}/v2/reports/{aliasId}",
            response.Headers.Location?.OriginalString);
    }

    [Fact(DisplayName = "POST /v2/reports без title: 400 model_state_validation_error")]
    public async Task CreateReportWithoutTitle()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PostAsJsonAsync("/v2/reports", new { title = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await ValidationProblemDetailsContract.AssertSingleErrorAsync(
            response,
            "title",
            "The title field is required.",
            "The field title must be a string with a minimum length of 1 and a maximum length of 128.");
    }

    /// <summary>
    /// В отличие от списка, GET репорта грузит и отдаёт всё дерево: ссылки, баги,
    /// комментарии, шаги и вложения всех трёх контекстов. Это и есть разница между
    /// двумя формами (см. <see cref="ListReportsOmitsKeysItDoesNotLoad"/>), поэтому
    /// сид полный, а проверяется присутствие каждой ветки с нашими идентификаторами.
    /// </summary>
    [Fact(DisplayName = "GET /v2/reports/{aliasId}: 200 и всё дерево репорта")]
    public async Task GetReport()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var commentId = await scenario.CreateCommentAsync(reportId, bugId);
        var stepId = await scenario.CreateStepAsync(reportId, bugId);
        await scenario.UploadBugAttachmentAsync(reportId, bugId);
        await scenario.UploadCommentAttachmentAsync(reportId, bugId, commentId);
        await scenario.UploadBugStepAttachmentAsync(reportId, bugId, stepId);
        var linkId = await scenario.CreateLinkAsync(reportId);

        var response = await scenario.Client.GetAsync($"/v2/reports/{reportId}");

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(reportId, body.GetProperty("id").GetString());
        Assert.Equal(linkId, Single(body.GetProperty("links")).GetProperty("id").GetInt32());

        var bug = FindBug(body.GetProperty("bugs").EnumerateArray().ToArray(), bugId);
        Assert.NotEmpty(bug.GetProperty("attachments").EnumerateArray().ToArray());

        var comment = Single(bug.GetProperty("comments"));
        Assert.Equal(commentId, comment.GetProperty("id").GetInt32());
        Assert.NotEmpty(comment.GetProperty("attachments").EnumerateArray().ToArray());

        var step = Single(bug.GetProperty("steps"));
        Assert.Equal(stepId, step.GetProperty("id").GetInt32());
        Assert.NotEmpty(step.GetProperty("attachments").EnumerateArray().ToArray());
    }

    /// <summary>
    /// Инвариант `required` = присутствие ключа: у бага с одним заполненным полем
    /// из пары `receive`/`expect` второй ключ обязан быть в объекте — со значением
    /// `null`, а не исчезать (см. `required` + `nullable` в контракте).
    /// </summary>
    [Fact(DisplayName = "GET /v2/reports/{aliasId}: у бага с одним полем оба ключа присутствуют, пустой — null")]
    public async Task GetReportKeepsBothNullableBugKeys()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var receiveOnlyId = await scenario.CreateOneFieldBugAsync(reportId, receive: "только факт");
        var expectOnlyId = await scenario.CreateOneFieldBugAsync(reportId, expect: "только ожидание");

        var response = await scenario.Client.GetAsync($"/v2/reports/{reportId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ContractScenario.ReadJsonAsync(response);
        var bugs = body.GetProperty("bugs").EnumerateArray().ToArray();

        var receiveOnly = FindBug(bugs, receiveOnlyId);
        Assert.Equal("только факт", receiveOnly.GetProperty("receive").GetString());
        Assert.Equal(JsonValueKind.Null, receiveOnly.GetProperty("expect").ValueKind);
        Assert.Equal(JsonValueKind.Null, receiveOnly.GetProperty("title").ValueKind);

        var expectOnly = FindBug(bugs, expectOnlyId);
        Assert.Equal("только ожидание", expectOnly.GetProperty("expect").GetString());
        Assert.Equal(JsonValueKind.Null, expectOnly.GetProperty("receive").ValueKind);
        Assert.Equal(JsonValueKind.Null, expectOnly.GetProperty("title").ValueKind);
    }

    /// <summary>
    /// Вложения всех трёх контекстов лежат в одной таблице и группируются по
    /// <c>entity_id</c>, а идентификаторы багов, комментариев и шагов — независимые
    /// последовательности. Единственное, что разводит их по владельцам, — <c>attach_type</c>.
    /// Тест фиксирует значения провода: 0 — факт бага, 2 — комментарий, 3 — шаг.
    /// </summary>
    [Fact(DisplayName = "GET /v2/reports/{aliasId}: attach_type и entity_id вложения совпадают с владельцем")]
    public async Task GetReportKeepsAttachmentOwnership()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var commentId = await scenario.CreateCommentAsync(reportId, bugId);
        var stepId = await scenario.CreateStepAsync(reportId, bugId);
        await scenario.UploadBugAttachmentAsync(reportId, bugId);
        await scenario.UploadCommentAttachmentAsync(reportId, bugId, commentId);
        await scenario.UploadBugStepAttachmentAsync(reportId, bugId, stepId);

        var response = await scenario.Client.GetAsync($"/v2/reports/{reportId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ContractScenario.ReadJsonAsync(response);
        var bug = FindBug(body.GetProperty("bugs").EnumerateArray().ToArray(), bugId);

        AssertAttachment(Single(bug.GetProperty("attachments")), expectedType: 0, expectedEntityId: bugId);

        var comment = Single(bug.GetProperty("comments"));
        Assert.Equal(commentId, comment.GetProperty("id").GetInt32());
        AssertAttachment(Single(comment.GetProperty("attachments")), expectedType: 2, expectedEntityId: commentId);

        var step = Single(bug.GetProperty("steps"));
        Assert.Equal(stepId, step.GetProperty("id").GetInt32());
        AssertAttachment(Single(step.GetProperty("attachments")), expectedType: 3, expectedEntityId: stepId);
    }


    /// <summary>
    /// Вложение внутри репорта отдаётся публичной формой <c>AttachmentSummary</c>:
    /// служебные поля хранилища (<c>storage_key</c>, <c>storage_kind</c>,
    /// <c>length_bytes</c>, <c>mime_type</c>, <c>is_gzip_compressed</c>) наружу
    /// не уходят. Проверяется поимённо и во всех трёх контекстах сразу
    /// (баг, комментарий, шаг).
    /// </summary>
    [Fact(DisplayName = "GET /v2/reports/{aliasId}: вложения отдают только публичные поля")]
    public async Task GetReportHidesAttachmentStorageFields()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var commentId = await scenario.CreateCommentAsync(reportId, bugId);
        var stepId = await scenario.CreateStepAsync(reportId, bugId);
        await scenario.UploadBugAttachmentAsync(reportId, bugId);
        await scenario.UploadCommentAttachmentAsync(reportId, bugId, commentId);
        await scenario.UploadBugStepAttachmentAsync(reportId, bugId, stepId);

        var response = await scenario.Client.GetAsync($"/v2/reports/{reportId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ContractScenario.ReadJsonAsync(response);
        var bug = FindBug(body.GetProperty("bugs").EnumerateArray().ToArray(), bugId);

        AssertPublicAttachmentShape(Single(bug.GetProperty("attachments")));
        AssertPublicAttachmentShape(Single(Single(bug.GetProperty("comments")).GetProperty("attachments")));
        AssertPublicAttachmentShape(Single(Single(bug.GetProperty("steps")).GetProperty("attachments")));
    }

    [Fact(DisplayName = "GET /v2/reports/{aliasId}: чужой репорт не отдаётся")]
    public async Task GetForeignReport()
    {
        var owner = ContractScenario.Create(fixture);
        var stranger = ContractScenario.Create(fixture);
        var reportId = await owner.CreateReportAsync();

        var response = await stranger.Client.GetAsync($"/v2/reports/{reportId}");

        await ContractResponse.ProblemAsync(response, "report_not_found", HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "PATCH /v2/reports/{aliasId}: 200 и ReportPatchResult с новым title")]
    public async Task PatchReport()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        var response = await scenario.Client.PatchAsJsonAsync(
            $"/v2/reports/{reportId}",
            new { title = "переименовали", is_excluded_from_analytics = true });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(reportId, body.GetProperty("id").GetString());
        Assert.Equal("переименовали", body.GetProperty("title").GetString());
    }

    /// <summary>
    /// Список отдаёт свой счётчик и элементы, у репорта без багов коллекция пустая,
    /// а не отсутствует: фронт рисует её без проверки на <c>null</c>.
    /// </summary>
    [Fact(DisplayName = "GET /v2/reports: 200, total и элементы списка")]
    public async Task ListReports()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        await scenario.CreateBugAsync(reportId);
        var emptyReportId = await scenario.CreateReportAsync("contract-report-без-багов");

        var response = await scenario.Client.GetAsync("/v2/reports?skip=0&take=10");

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        var reports = body.GetProperty("reports").EnumerateArray().ToArray();
        Assert.Equal(reports.Length, body.GetProperty("total").GetInt32());

        Assert.NotEmpty(FindReport(reports, reportId).GetProperty("bugs").EnumerateArray().ToArray());
        Assert.Empty(FindReport(reports, emptyReportId).GetProperty("bugs").EnumerateArray().ToArray());
    }

    /// <summary>
    /// LIST не загружает ссылки репорта, вложения багов и шаги воспроизведения
    /// (см. <c>ReportsDbClient.ListReportsAsync</c>), и раньше отдавал их наружу
    /// как `null`. Теперь у элемента списка своя форма — ключей нет вовсе.
    /// </summary>
    [Fact(DisplayName = "GET /v2/reports: в элементе списка нет links, вложений бага и шагов")]
    public async Task ListReportsOmitsKeysItDoesNotLoad()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var commentId = await scenario.CreateCommentAsync(reportId, bugId);
        var stepId = await scenario.CreateStepAsync(reportId, bugId);
        await scenario.UploadBugAttachmentAsync(reportId, bugId);
        await scenario.UploadBugStepAttachmentAsync(reportId, bugId, stepId);
        await scenario.CreateLinkAsync(reportId);

        var response = await scenario.Client.GetAsync("/v2/reports?skip=0&take=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ContractScenario.ReadJsonAsync(response);
        var report = Assert.Single(
            body.GetProperty("reports").EnumerateArray().ToArray(),
            item => item.GetProperty("id").GetString() == reportId);

        Assert.False(report.TryGetProperty("links", out _));

        var bug = FindBug(report.GetProperty("bugs").EnumerateArray().ToArray(), bugId);
        Assert.False(bug.TryGetProperty("attachments", out _));
        Assert.False(bug.TryGetProperty("steps", out _));

        // Комментарии список грузит и фронт их читает — они остаются.
        var comment = Single(bug.GetProperty("comments"));
        Assert.Equal(commentId, comment.GetProperty("id").GetInt32());
    }

    [Fact(DisplayName = "GET /v2/reports с take вне диапазона: 400")]
    public async Task ListReportsWithInvalidTake()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync("/v2/reports?skip=0&take=1000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await ValidationProblemDetailsContract.AssertSingleErrorAsync(
            response,
            "take",
            "The field take must be between 1 and 100.");
    }

    [Fact(DisplayName = "POST /v2/reports/counts:batch: вложенный ключ errors использует wire-путь scopes[0].key")]
    public async Task CountsBatchWithoutNestedScopeKey()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PostAsJsonAsync(
            "/v2/reports/counts:batch",
            new { scopes = new[] { new { statuses = new[] { 0 } } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await ValidationProblemDetailsContract.AssertSingleErrorAsync(
            response,
            "scopes[0].key",
            "The key field is required.");
    }

    [Fact(DisplayName = "GET /v2/reports/legacy/{legacyId}: teamId + teamReportId для редиректа")]
    public async Task ResolveLegacyReport()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        var response = await scenario.Client.GetAsync($"/v2/reports/legacy/{reportId}");

        // Фронт собирает из этой пары адрес нового URL репорта, поэтому оба поля
        // обязаны приехать заполненными: без team_id редирект собрать не из чего.
        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(scenario.TeamId, body.GetProperty("team_id").GetString());
        Assert.True(body.GetProperty("team_report_id").GetInt32() > 0);
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

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        var count = Single(body.GetProperty("counts"));
        Assert.Equal("all", count.GetProperty("key").GetString());
        Assert.Equal(1, count.GetProperty("count").GetInt32());
    }

    [Fact(DisplayName = "POST /v2/reports/counts:batch с дублем ключа: 400 duplicate_scope_key")]
    public async Task CountsBatchWithDuplicateKey()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.PostAsJsonAsync(
            "/v2/reports/counts:batch",
            new { scopes = new[] { new { key = "all" }, new { key = "all" } } });

        // `key` в теле отказа — прикладное расширение поверх общего каталога: фронт
        // показывает, какой именно ключ повторился.
        var problem = await ContractResponse.ProblemAsync(
            response,
            "duplicate_scope_key",
            HttpStatusCode.BadRequest);
        Assert.Equal("all", problem.GetProperty("key").GetString());
    }

    private static JsonElement FindReport(JsonElement[] reports, string reportId) =>
        Assert.Single(reports, report => report.GetProperty("id").GetString() == reportId);

    private static JsonElement FindBug(JsonElement[] bugs, int bugId) =>
        Assert.Single(bugs, bug => bug.GetProperty("id").GetInt32() == bugId);

    private static JsonElement Single(JsonElement array) =>
        Assert.Single(array.EnumerateArray().ToArray());

    private static void AssertAttachment(JsonElement attachment, int expectedType, int expectedEntityId)
    {
        Assert.Equal(expectedType, attachment.GetProperty("attach_type").GetInt32());
        Assert.Equal(expectedEntityId, attachment.GetProperty("entity_id").GetInt32());
    }

    private static void AssertPublicAttachmentShape(JsonElement attachment)
    {
        Assert.Equal(
            new[]
            {
                "attach_type", "created_at", "creator_user_id", "entity_id",
                "file_name", "has_preview", "id",
            },
            attachment.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
    }
}
