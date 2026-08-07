using System.Net;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Кнопка «Исправить баг» со стороны backend: 202, системный комментарий-маркер
/// с доставкой по realtime и асинхронный сигнал раннеру. Раннер в тестах —
/// записывающий адаптер: проверяется, что ушло бы наружу, без HTTP-приёмника.
/// </summary>
[Collection("PostgresCollection")]
public sealed class BugFixRequestContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    [Fact(DisplayName = "POST .../fix-request: 202, маркер в баге, realtime и вебхук без секретов")]
    public async Task FixRequestLeavesMarkerAndNotifiesRunner()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var notifier = fixture.Services.GetRequiredService<RecordingBugFixRequestedNotifier>();
        var hub = fixture.Services.GetRequiredService<FakeReportPageHubClient>();

        var response = await scenario.Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/fix-request", null);

        await ContractResponse.EmptyAsync(response, HttpStatusCode.Accepted);

        // Маркер лежит в баге как системный комментарий и виден фронту.
        var report = await ContractScenario.ReadJsonAsync(
            await scenario.Client.GetAsync($"/v2/reports/{reportId}"));
        var comment = Assert.Single(report.GetProperty("bugs")[0].GetProperty("comments").EnumerateArray());
        Assert.Equal("system", comment.GetProperty("creator_type").GetString());
        Assert.Contains("запрошено исправление", comment.GetProperty("text").GetString());

        // Realtime-пуш комментария ушёл той же группе, что у остальных лог-комментариев.
        Assert.Contains(hub.CommentCreates, c => c.Comment.BugId == bugId);

        // Вебхук получил идентификаторы и путь — и ничего похожего на секрет.
        var payload = Assert.Single(notifier.Payloads, p => p.BugId == bugId);
        Assert.Equal(reportId, payload.ReportId);
        Assert.Equal(scenario.WorkspaceId, payload.WorkspaceId);
        Assert.Equal(scenario.TeamId, payload.TeamId);
        Assert.Equal(scenario.UserId, payload.RequestedByUserId);
        Assert.Equal(
            $"/api/app/workspaces/{scenario.WorkspaceId}/teams/{scenario.TeamId}/v2/reports/{reportId}",
            payload.ReportPath);
    }

    [Fact(DisplayName = "Повтор в кулдауне: 202, но второй маркер и вебхук не создаются")]
    public async Task RepeatWithinCooldownIsIdempotent()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var notifier = fixture.Services.GetRequiredService<RecordingBugFixRequestedNotifier>();

        var first = await scenario.Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/fix-request", null);
        var second = await scenario.Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/fix-request", null);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);

        var report = await ContractScenario.ReadJsonAsync(
            await scenario.Client.GetAsync($"/v2/reports/{reportId}"));
        Assert.Single(report.GetProperty("bugs")[0].GetProperty("comments").EnumerateArray());
        Assert.Single(notifier.Payloads, p => p.BugId == bugId);
    }

    [Fact(DisplayName = "Чужое рабочее пространство: 404, ни маркера, ни вебхука")]
    public async Task ForeignWorkspaceCannotRequestFix()
    {
        var owner = ContractScenario.Create(fixture);
        var reportId = await owner.CreateReportAsync();
        var bugId = await owner.CreateBugAsync(reportId);
        var stranger = ContractScenario.Create(fixture);
        var notifier = fixture.Services.GetRequiredService<RecordingBugFixRequestedNotifier>();

        var response = await stranger.Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/fix-request", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(notifier.Payloads, p => p.BugId == bugId);

        var report = await ContractScenario.ReadJsonAsync(
            await owner.Client.GetAsync($"/v2/reports/{reportId}"));
        Assert.Empty(report.GetProperty("bugs")[0].GetProperty("comments").EnumerateArray());
    }

    [Fact(DisplayName = "Несуществующий баг: 404")]
    public async Task UnknownBugIsNotFound()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        var response = await scenario.Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/999999/fix-request", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
