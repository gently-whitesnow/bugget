using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Инструменты шагов воспроизведения MCP: <c>create_bug_step</c>,
/// <c>update_bug_step</c>, <c>delete_bug_step</c>. Идентичность и изоляция —
/// те же, что у остальных write-инструментов (<see cref="McpWriteToolsContractTests"/>);
/// здесь — поведение самих шагов: нумерация по порядку вызовов, замена текста,
/// удаление без перенумерации, границы длины и чужого workspace.
/// </summary>
[Collection("PostgresCollection")]
public sealed class McpBugStepToolsContractTests(AppContractFixture fixture)
    : IClassFixture<AppContractFixture>, IAsyncDisposable
{
    private readonly List<HttpClientTransport> _transports = [];

    [Fact(DisplayName = "create_bug_step: шаги нумеруются по порядку вызовов и видны и в MCP, и в REST")]
    public async Task CreateBugStepNumbersStepsInCallOrder()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        await using var client = await CreateMcpClientAsync(scenario);
        var first = await CallAsync(
            client,
            "create_bug_step",
            Args(("reportId", reportId), ("bugId", bugId), ("text", "открыть экран настроек")));
        var second = await CallAsync(
            client,
            "create_bug_step",
            Args(("reportId", reportId), ("bugId", bugId), ("text", "нажать «Сохранить» дважды")));

        Assert.Equal(1, first.GetProperty("step_number").GetInt32());
        Assert.Equal(2, second.GetProperty("step_number").GetInt32());
        Assert.Equal("открыть экран настроек", first.GetProperty("text").GetString());

        // Шаги, заведённые через MCP, — это шаги того же бага, который видит фронт.
        var rest = await ContractScenario.ReadJsonAsync(
            await scenario.Client.GetAsync($"/v2/reports/{reportId}"));
        var restSteps = Single(rest, "bugs").GetProperty("steps").EnumerateArray().ToArray();
        Assert.Equal(2, restSteps.Length);
        Assert.Equal(
            new[] { "открыть экран настроек", "нажать «Сохранить» дважды" },
            restSteps.OrderBy(s => s.GetProperty("step_number").GetInt32())
                .Select(s => s.GetProperty("text").GetString()).ToArray());

        // И в дереве get_report — там, где шаги читает модель.
        var report = await CallAsync(client, "get_report", Args(("reportId", reportId)));
        var mcpSteps = Single(report, "bugs").GetProperty("steps").EnumerateArray().ToArray();
        Assert.Equal(2, mcpSteps.Length);
    }

    [Fact(DisplayName = "update_bug_step: текст заменяется, номер шага сохраняется")]
    public async Task UpdateBugStepReplacesText()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        await using var client = await CreateMcpClientAsync(scenario);
        var created = await CallAsync(
            client,
            "create_bug_step",
            Args(("reportId", reportId), ("bugId", bugId), ("text", "первая редакция")));

        var updated = await CallAsync(
            client,
            "update_bug_step",
            Args(
                ("reportId", reportId),
                ("bugId", bugId),
                ("stepId", created.GetProperty("id").GetInt32()),
                ("text", "вторая редакция")));

        Assert.Equal(created.GetProperty("id").GetInt32(), updated.GetProperty("id").GetInt32());
        Assert.Equal(created.GetProperty("step_number").GetInt32(), updated.GetProperty("step_number").GetInt32());
        Assert.Equal("вторая редакция", updated.GetProperty("text").GetString());
    }

    [Fact(DisplayName = "delete_bug_step: шаг исчезает из дерева репорта, номера оставшихся не меняются")]
    public async Task DeleteBugStepRemovesStepKeepingRestNumbers()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        await using var client = await CreateMcpClientAsync(scenario);
        var first = await CallAsync(
            client,
            "create_bug_step",
            Args(("reportId", reportId), ("bugId", bugId), ("text", "лишний шаг")));
        await CallAsync(
            client,
            "create_bug_step",
            Args(("reportId", reportId), ("bugId", bugId), ("text", "нужный шаг")));

        var deleted = await CallAsync(
            client,
            "delete_bug_step",
            Args(("reportId", reportId), ("bugId", bugId), ("stepId", first.GetProperty("id").GetInt32())));

        Assert.Equal(first.GetProperty("id").GetInt32(), deleted.GetProperty("deleted_step_id").GetInt32());

        var report = await CallAsync(client, "get_report", Args(("reportId", reportId)));
        var step = Single(Single(report, "bugs"), "steps");
        Assert.Equal("нужный шаг", step.GetProperty("text").GetString());
        // Перенумерации при удалении нет: второй шаг остаётся под своим номером.
        Assert.Equal(2, step.GetProperty("step_number").GetInt32());
    }

    [Fact(DisplayName = "Чужое рабочее пространство: инструменты шагов не достают до репорта")]
    public async Task ForeignWorkspaceStepsAreNotWritable()
    {
        var owner = ContractScenario.Create(fixture);
        var foreignReportId = await owner.CreateReportAsync();
        var foreignBugId = await owner.CreateBugAsync(foreignReportId);
        var foreignStepId = await owner.CreateStepAsync(foreignReportId, foreignBugId);

        await using var client = await CreateMcpClientAsync(ContractScenario.Create(fixture));

        await AssertToolFailsAsync(
            client,
            "create_bug_step",
            Args(("reportId", foreignReportId), ("bugId", foreignBugId), ("text", "не должно записаться")));
        await AssertToolFailsAsync(
            client,
            "update_bug_step",
            Args(
                ("reportId", foreignReportId),
                ("bugId", foreignBugId),
                ("stepId", foreignStepId),
                ("text", "не должно записаться")));
        await AssertToolFailsAsync(
            client,
            "delete_bug_step",
            Args(("reportId", foreignReportId), ("bugId", foreignBugId), ("stepId", foreignStepId)));

        // Чужой шаг цел: один и с исходным текстом.
        var rest = await ContractScenario.ReadJsonAsync(
            await owner.Client.GetAsync($"/v2/reports/{foreignReportId}"));
        var step = Single(Single(rest, "bugs"), "steps");
        Assert.Equal(foreignStepId, step.GetProperty("id").GetInt32());
        Assert.NotEqual("не должно записаться", step.GetProperty("text").GetString());
    }

    [Fact(DisplayName = "Текст шага длиннее лимита REST: отказ, а не запись")]
    public async Task OversizedStepTextIsRejected()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        await using var client = await CreateMcpClientAsync(scenario);
        await AssertToolFailsAsync(
            client,
            "create_bug_step",
            Args(("reportId", reportId), ("bugId", bugId), ("text", new string('в', 2049))));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var transport in _transports)
        {
            await transport.DisposeAsync();
        }
    }

    /// <summary>
    /// Та же identity вида «после /_internal/auth по PAT», что и у остальных
    /// write-инструментов: шаги — часть одной write-поверхности агента.
    /// </summary>
    private async Task<McpClient> CreateMcpClientAsync(ContractScenario scenario)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(fixture.BaseAddress, "/v1/mcp"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    [ContractHeaders.UserId] = scenario.UserId,
                    [ContractHeaders.TeamId] = scenario.TeamId,
                    [ContractHeaders.WorkspaceId] = scenario.WorkspaceId,
                    [ContractHeaders.WorkspaceRole] = "owner",
                    [ContractHeaders.AuthMethod] = "pat",
                },
            },
            fixture.CreateAnonymousClient(),
            loggerFactory: null,
            ownsHttpClient: true);

        _transports.Add(transport);

        return await McpClient.CreateAsync(transport);
    }

    private static async Task<JsonElement> CallAsync(
        McpClient client,
        string tool,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var result = await client.CallToolAsync(tool, arguments);
        var text = TextOf(result);

        Assert.True(result.IsError != true, $"{tool} вернул ошибку: {text}");

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task<string> AssertToolFailsAsync(
        McpClient client,
        string tool,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var result = await client.CallToolAsync(tool, arguments);
        var text = TextOf(result);

        Assert.True(result.IsError == true, $"{tool} обязан был отказать, но ответил: {text}");

        return text;
    }

    private static string TextOf(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    private static Dictionary<string, object?> Args(params (string Name, object Value)[] arguments) =>
        arguments.ToDictionary(argument => argument.Name, argument => (object?)argument.Value);

    private static JsonElement Single(JsonElement parent, string property) =>
        parent.GetProperty(property).EnumerateArray().Single();
}
