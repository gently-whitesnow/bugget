using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контракт багов и шагов воспроизведения: <c>/v2/reports/{aliasId}/bugs/**</c>.
/// </summary>
[Collection("PostgresCollection")]
public sealed class BugsContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    [Fact(DisplayName = "POST /v2/reports/{aliasId}/bugs: 201 и форма BugSummary")]
    public async Task CreateBug()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        var response = await scenario.Client.PostAsJsonAsync(
            $"/v2/reports/{reportId}/bugs",
            new { title = "баг", receive = "получили это", expect = "ожидали то" });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.Created);
        Assert.True(body.GetProperty("id").GetInt32() > 0);
        Assert.Equal("баг", body.GetProperty("title").GetString());
        Assert.Equal("получили это", body.GetProperty("receive").GetString());
        Assert.Equal("ожидали то", body.GetProperty("expect").GetString());
    }

    [Fact(DisplayName = "POST .../bugs без receive и expect: 400 доменной ошибкой")]
    public async Task CreateBugWithoutFields()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        var response = await scenario.Client.PostAsJsonAsync(
            $"/v2/reports/{reportId}/bugs",
            new { title = "баг" });

        await ContractResponse.ProblemAsync(response, "bug_must_have_one_field", HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Ответ на создание — `BugSummary`, и он тоже обязан отдавать оба ключа пары
    /// `receive`/`expect`: бизнес-правило требует заполнить одно из двух, а не оба,
    /// и незаполненное уходит наружу как `null`, а не исчезает из тела.
    /// </summary>
    [Fact(DisplayName = "POST .../bugs с одним заполненным полем: 201, второй ключ приходит null")]
    public async Task CreateBugWithOneFilledField()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        var response = await scenario.Client.PostAsJsonAsync(
            $"/v2/reports/{reportId}/bugs",
            new { receive = "только факт" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await ContractScenario.ReadJsonAsync(response);
        Assert.Equal("только факт", body.GetProperty("receive").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("expect").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("title").ValueKind);
    }

    [Fact(DisplayName = "PATCH /v2/reports/{aliasId}/bugs/{bugId}: 200 и форма BugPatchResult")]
    public async Task PatchBug()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        var response = await scenario.Client.PatchAsJsonAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}",
            new { title = "переименовали", status = "verified" });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(bugId, body.GetProperty("id").GetInt32());
        Assert.Equal("переименовали", body.GetProperty("title").GetString());
        Assert.Equal("verified", body.GetProperty("status").GetString());
    }

    /// <summary>
    /// `patch_bug_internal` возвращает `receive`/`expect` как есть, а не «как передали»:
    /// у бага с одним заполненным полем второе остаётся `NULL` (миграция 009 сняла
    /// `NOT NULL` с обеих колонок). Ключ при этом присутствует со значением `null` —
    /// отсюда `required` + `nullable` у `BugPatchResult`.
    /// </summary>
    [Fact(DisplayName = "PATCH .../bugs/{bugId} бага с одним заполненным полем: второй ключ приходит null")]
    public async Task PatchBugWithOneFilledField()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateOneFieldBugAsync(reportId, receive: "только факт");

        var response = await scenario.Client.PatchAsJsonAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}",
            new { status = "verified" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ContractScenario.ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("expect").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("title").ValueKind);
        Assert.Equal("только факт", body.GetProperty("receive").GetString());
    }

    /// <summary>
    /// Зеркало предыдущего сценария: незаполненным остаётся `receive`. Плюс проверка,
    /// что PATCH второго поля его действительно заполняет, а не оставляет `null`, —
    /// иначе «оба ключа всегда есть» держалось бы только на одной ветке.
    /// </summary>
    [Fact(DisplayName = "PATCH .../bugs/{bugId} бага только с expect: receive приходит null и заполняется патчем")]
    public async Task PatchBugWithOnlyExpectFilled()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateOneFieldBugAsync(reportId, expect: "только ожидание");

        var patchedStatus = await scenario.Client.PatchAsJsonAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}",
            new { status = "verified" });

        Assert.Equal(HttpStatusCode.OK, patchedStatus.StatusCode);
        var afterStatus = await ContractScenario.ReadJsonAsync(patchedStatus);
        Assert.Equal("только ожидание", afterStatus.GetProperty("expect").GetString());
        Assert.Equal(JsonValueKind.Null, afterStatus.GetProperty("receive").ValueKind);

        var patchedReceive = await scenario.Client.PatchAsJsonAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}",
            new { receive = "дозаполнили факт" });

        Assert.Equal(HttpStatusCode.OK, patchedReceive.StatusCode);
        var afterReceive = await ContractScenario.ReadJsonAsync(patchedReceive);
        Assert.Equal("дозаполнили факт", afterReceive.GetProperty("receive").GetString());
        Assert.Equal("только ожидание", afterReceive.GetProperty("expect").GetString());
    }

    [Fact(DisplayName = "POST .../steps: 201 и форма BugStepSummary")]
    public async Task CreateStep()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        var response = await scenario.Client.PostAsJsonAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/steps",
            new { text = "открыть страницу" });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.Created);
        Assert.Equal(bugId, body.GetProperty("bug_id").GetInt32());
        Assert.Equal("открыть страницу", body.GetProperty("text").GetString());
    }

    [Fact(DisplayName = "PATCH .../steps/{stepId}: 200 и форма BugStepSummary")]
    public async Task PatchStep()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var stepId = await scenario.CreateStepAsync(reportId, bugId);

        var response = await scenario.Client.PatchAsJsonAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/steps/{stepId}",
            new { text = "переписали шаг" });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(stepId, body.GetProperty("id").GetInt32());
        Assert.Equal("переписали шаг", body.GetProperty("text").GetString());
    }

    [Fact(DisplayName = "PUT .../steps/order: 200 и массив шагов в новом порядке")]
    public async Task ReorderSteps()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var first = await scenario.CreateStepAsync(reportId, bugId, "шаг 1");
        var second = await scenario.CreateStepAsync(reportId, bugId, "шаг 2");

        var response = await scenario.Client.PutAsJsonAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/steps/order",
            new { step_ids = new[] { second, first } });

        // Ответ — сами шаги в новом порядке: фронт перерисовывает список по нему,
        // а не по тому, что отправил.
        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(
            new[] { second, first },
            body.EnumerateArray().Select(step => step.GetProperty("id").GetInt32()).ToArray());
        Assert.Equal(
            new[] { 1, 2 },
            body.EnumerateArray().Select(step => step.GetProperty("step_number").GetInt32()).ToArray());
    }

    [Fact(DisplayName = "DELETE .../steps/{stepId}: 200 и пустое тело")]
    public async Task DeleteStep()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var stepId = await scenario.CreateStepAsync(reportId, bugId);

        var response = await scenario.Client.DeleteAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/steps/{stepId}");

        await ContractResponse.EmptyAsync(response, HttpStatusCode.OK);
    }
}
