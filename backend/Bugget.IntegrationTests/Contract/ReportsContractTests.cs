using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        var commentId = await scenario.CreateCommentAsync(reportId, bugId);
        var stepId = await scenario.CreateStepAsync(reportId, bugId);
        await scenario.UploadBugAttachmentAsync(reportId, bugId);
        await scenario.UploadCommentAttachmentAsync(reportId, bugId, commentId);
        await scenario.UploadBugStepAttachmentAsync(reportId, bugId, stepId);
        await scenario.CreateLinkAsync(reportId);
        await scenario.CreateOneFieldBugAsync(reportId, receive: "только факт");
        await scenario.CreateOneFieldBugAsync(reportId, expect: "только ожидание");

        var response = await scenario.Client.GetAsync($"/v2/reports/{reportId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.reports.get", response);
    }

    /// <summary>
    /// Снимок сливает элементы массива в одну строку и поэтому не отличает «ключ есть
    /// со значением `null`» от «ключа нет у этого элемента». Инвариант `required` =
    /// присутствие ключа проверяется здесь поэлементно: у бага с одним заполненным
    /// полем из пары `receive`/`expect` второй ключ обязан быть в объекте — со
    /// значением `null`.
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
    /// не уходят. Снимок ловит их появление, а этот тест — поимённо и во всех трёх
    /// контекстах сразу (баг, комментарий, шаг).
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

    /// <summary>
    /// Сид намеренно полный и с данными, которые LIST не отдаёт: ссылка, вложения
    /// бага/комментария/шага и сам шаг лежат в базе, но в списке их быть не должно —
    /// снимок это и доказывает. Второй репорт без багов держит форму пустой
    /// коллекции, баг с одним полем из пары `receive`/`expect` — `null` в ключе.
    /// </summary>
    [Fact(DisplayName = "GET /v2/reports: 200, total + reports")]
    public async Task ListReports()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var commentId = await scenario.CreateCommentAsync(reportId, bugId);
        var stepId = await scenario.CreateStepAsync(reportId, bugId);
        await scenario.UploadBugAttachmentAsync(reportId, bugId);
        await scenario.UploadCommentAttachmentAsync(reportId, bugId, commentId);
        await scenario.UploadBugStepAttachmentAsync(reportId, bugId, stepId);
        await scenario.CreateLinkAsync(reportId);
        await scenario.CreateOneFieldBugAsync(reportId, receive: "только факт");
        await scenario.CreateReportAsync("contract-report-без-багов");

        var response = await scenario.Client.GetAsync("/v2/reports?skip=0&take=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.reports.list", response);
    }

    /// <summary>
    /// LIST не загружает ссылки репорта, вложения багов и шаги воспроизведения
    /// (см. <c>ReportsDbClient.ListReportsAsync</c>), и раньше отдавал их наружу
    /// как `null`. Теперь у элемента списка своя форма — ключей нет вовсе.
    /// Снимок сливает элементы массива и не отличает «ключ есть со значением null»
    /// от «ключа нет», поэтому отсутствие проверяется здесь поэлементно.
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
