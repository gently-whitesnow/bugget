using System.Net;
using System.Net.Http.Json;
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

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.bugs.post", response);
    }

    [Fact(DisplayName = "POST .../bugs без receive и expect: 400 доменной ошибкой")]
    public async Task CreateBugWithoutFields()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        var response = await scenario.Client.PostAsJsonAsync(
            $"/v2/reports/{reportId}/bugs",
            new { title = "баг" });

        await ContractSnapshot.MatchAsync("v2.bugs.post.empty", response);
    }

    [Fact(DisplayName = "PATCH /v2/reports/{aliasId}/bugs/{bugId}: 200 и форма BugPatchResult")]
    public async Task PatchBug()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        var response = await scenario.Client.PatchAsJsonAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}",
            new { title = "переименовали", status = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.bugs.patch", response);
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

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.bug-steps.post", response);
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.bug-steps.patch", response);
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.bug-steps.order.put", response);
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.bug-steps.delete", response);
    }
}
