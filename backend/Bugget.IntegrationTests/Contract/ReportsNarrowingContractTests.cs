using System.Net;
using System.Text.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>Сужение публичной формы <c>reports</c> (ADR-0005): карточка — вложения без полей хранилища, элемент
/// списка — без ссылок, вложений бага и шагов. Проверяется то, чего не видит снимок: он сливает элементы массива
/// в объединение путей и не отличает «ключа нет» от <c>null</c>, не ловит лишний ключ у элемента.</summary>
[Collection("PostgresCollection")]
public sealed class ReportsNarrowingContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    /// <summary><c>attach_type = 1</c> — единственное значение 0..3, которого нет в остальных тестах (сид грузит
    /// только факт): тип должен доехать до провода как есть, а не подмениться на 0.</summary>
    [Fact(DisplayName = "GET /v2/reports/{aliasId}: вложение expect отдаётся с attach_type = 1")]
    public async Task GetReportKeepsExpectedAttachmentType()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var factId = await scenario.UploadBugAttachmentAsync(reportId, bugId, "fact.png");
        var expectedId = await scenario.UploadBugAttachmentAsync(reportId, bugId, "expected.png", attachType: "expected");

        var response = await scenario.Client.GetAsync($"/v2/reports/{reportId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ContractScenario.ReadJsonAsync(response);
        var bug = FindBug(body.GetProperty("bugs").EnumerateArray().ToArray(), bugId);
        var attachments = bug.GetProperty("attachments").EnumerateArray().ToArray();

        var fact = Assert.Single(attachments, item => item.GetProperty("id").GetInt32() == factId);
        AssertAttachment(fact, expectedType: "fact", expectedEntityId: bugId);
        AssertPublicAttachmentShape(fact);

        var expected = Assert.Single(attachments, item => item.GetProperty("id").GetInt32() == expectedId);
        AssertAttachment(expected, expectedType: "expected", expectedEntityId: bugId);
        AssertPublicAttachmentShape(expected);
    }

    /// <summary>Пустая коллекция и отсутствующий ключ — разные вещи на проводе: без ключа код,
    /// читающий <c>attachments.length</c>, падает.</summary>
    [Fact(DisplayName = "GET /v2/reports/{aliasId}: пустые вложения — пустые массивы, а не отсутствие ключа")]
    public async Task GetReportKeepsEmptyCollectionsAsArrays()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var commentId = await scenario.CreateCommentAsync(reportId, bugId);
        var stepId = await scenario.CreateStepAsync(reportId, bugId);

        var response = await scenario.Client.GetAsync($"/v2/reports/{reportId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ContractScenario.ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("links").ValueKind);

        var bug = FindBug(body.GetProperty("bugs").EnumerateArray().ToArray(), bugId);
        AssertEmptyArray(bug, "attachments");

        var comment = Single(bug.GetProperty("comments"));
        Assert.Equal(commentId, comment.GetProperty("id").GetInt32());
        AssertEmptyArray(comment, "attachments");

        var step = Single(bug.GetProperty("steps"));
        Assert.Equal(stepId, step.GetProperty("id").GetInt32());
        AssertEmptyArray(step, "attachments");
    }

    /// <summary>Набор ключей сверяется поимённо и целиком: в снимке лишний ключ одного элемента растворяется.</summary>
    [Fact(DisplayName = "GET /v2/reports: набор ключей элемента списка и его бага — ровно контрактный")]
    public async Task ListReportsKeepsExactKeySet()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        await scenario.CreateCommentAsync(reportId, bugId);
        await scenario.CreateLinkAsync(reportId);
        await scenario.UploadBugAttachmentAsync(reportId, bugId);

        var response = await scenario.Client.GetAsync("/v2/reports?skip=0&take=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ContractScenario.ReadJsonAsync(response);
        var report = FindReport(body, reportId);

        AssertKeys(
            report,
            "bugs", "created_at", "creator_team_id", "creator_type", "creator_user_id",
            "id", "is_excluded_from_analytics", "participants_user_ids",
            "past_responsible_user_id", "responsible_user_id", "status", "title", "updated_at");

        AssertKeys(
            FindBug(report.GetProperty("bugs").EnumerateArray().ToArray(), bugId),
            "comments", "created_at", "creator_type", "creator_user_id", "expect",
            "id", "receive", "report_id", "status", "title", "updated_at");
    }

    /// <summary>Карточка в списке считает комментарии через <c>bugs[].comments.length</c> — ключ обязан быть массивом.</summary>
    [Fact(DisplayName = "GET /v2/reports: у бага без комментариев comments — пустой массив")]
    public async Task ListReportsKeepsEmptyCommentsAsArray()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        var response = await scenario.Client.GetAsync("/v2/reports?skip=0&take=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ContractScenario.ReadJsonAsync(response);
        var report = FindReport(body, reportId);

        AssertEmptyArray(FindBug(report.GetProperty("bugs").EnumerateArray().ToArray(), bugId), "comments");
    }

    private static JsonElement FindReport(JsonElement body, string reportId) =>
        Assert.Single(
            body.GetProperty("reports").EnumerateArray().ToArray(),
            item => item.GetProperty("id").GetString() == reportId);

    private static JsonElement FindBug(JsonElement[] bugs, int bugId) =>
        Assert.Single(bugs, bug => bug.GetProperty("id").GetInt32() == bugId);

    private static JsonElement Single(JsonElement array) =>
        Assert.Single(array.EnumerateArray().ToArray());

    private static void AssertEmptyArray(JsonElement owner, string key)
    {
        Assert.True(owner.TryGetProperty(key, out var value), $"ключ `{key}` пропал из ответа");
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        Assert.Empty(value.EnumerateArray());
    }

    private static void AssertKeys(JsonElement element, params string[] expected) =>
        Assert.Equal(
            expected.OrderBy(name => name, StringComparer.Ordinal),
            element.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));

    private static void AssertAttachment(JsonElement attachment, string expectedType, int expectedEntityId)
    {
        Assert.Equal(expectedType, attachment.GetProperty("attach_type").GetString());
        Assert.Equal(expectedEntityId, attachment.GetProperty("entity_id").GetInt32());
    }

    private static void AssertPublicAttachmentShape(JsonElement attachment) =>
        Assert.Equal(
            new[]
            {
                "attach_type", "created_at", "creator_user_id", "entity_id",
                "file_name", "has_preview", "id",
            },
            attachment.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
}
