using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// MCP-инструменты <c>create_report</c> и <c>create_bug</c> (kaiten 237700). Клиент ходит с PAT-identity:
/// созданное обязано ложиться в историю как действие агента, а изоляция workspace/team — не слабее REST.
/// </summary>
[Collection("PostgresCollection")]
public sealed class McpCreateToolsContractTests(AppContractFixture fixture)
    : IClassFixture<AppContractFixture>, IAsyncDisposable
{
    private McpPatClients? _mcp;

    private McpPatClients Mcp => _mcp ??= new McpPatClients(fixture);

    [Fact(DisplayName = "create_report: репорт заведён по PAT и виден фронту как действие агента")]
    public async Task CreateReportIsAttributedToAgent()
    {
        var scenario = ContractScenario.Create(fixture);

        await using var client = await Mcp.CreateAsync(scenario);
        var report = await CallAsync(client, "create_report", Args(("title", "баг нашёл агент")));

        var reportId = report.GetProperty("id").GetString()!;
        Assert.Equal("баг нашёл агент", report.GetProperty("title").GetString());
        Assert.Equal("agent", report.GetProperty("creator_type").GetString());
        Assert.Equal(scenario.UserId, report.GetProperty("creator_user_id").GetString());

        // Тот же репорт виден фронту, и там он тоже помечен агентом.
        var rest = await ContractScenario.ReadJsonAsync(await scenario.Client.GetAsync($"/v2/reports/{reportId}"));
        Assert.Equal("баг нашёл агент", rest.GetProperty("title").GetString());
        Assert.Equal("agent", rest.GetProperty("creator_type").GetString());
    }

    [Fact(DisplayName = "create_bug: баг добавлен в репорт и помечен агентом")]
    public async Task CreateBugAddsBugAttributedToAgent()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        await using var client = await Mcp.CreateAsync(scenario);
        var bug = await CallAsync(
            client,
            "create_bug",
            Args(
                ("reportId", reportId),
                ("title", "падает выгрузка"),
                ("receive", "ошибка 500"),
                ("expect", "скачивается файл")));

        var bugId = bug.GetProperty("id").GetInt32();
        Assert.Equal("падает выгрузка", bug.GetProperty("title").GetString());
        Assert.Equal("ошибка 500", bug.GetProperty("receive").GetString());
        Assert.Equal("agent", bug.GetProperty("creator_type").GetString());

        // Баг реально лёг в дерево репорта и там помечен агентом.
        var report = await CallAsync(client, "get_report", Args(("reportId", reportId)));
        var created = Single(report, "bugs");
        Assert.Equal(bugId, created.GetProperty("id").GetInt32());
        Assert.Equal("agent", created.GetProperty("creator_type").GetString());
    }

    [Fact(DisplayName = "create_bug без единого поля: отказ, а не пустой баг")]
    public async Task CreateBugRejectsEmpty()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        await using var client = await Mcp.CreateAsync(scenario);
        var error = await AssertToolFailsAsync(client, "create_bug", Args(("reportId", reportId)));

        Assert.Contains("receive", error, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "create_bug только с title: отказ на границе инструмента, тем же правилом, что и домен")]
    public async Task CreateBugRejectsTitleOnly()
    {
        // Домен (BugsService) требует receive или expect; инструмент обязан отказать тем же критерием.
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        await using var client = await Mcp.CreateAsync(scenario);
        var error = await AssertToolFailsAsync(
            client,
            "create_bug",
            Args(("reportId", reportId), ("title", "только заголовок")));

        Assert.Contains("receive", error, StringComparison.Ordinal);

        var report = await ContractScenario.ReadJsonAsync(await scenario.Client.GetAsync($"/v2/reports/{reportId}"));
        Assert.Empty(report.GetProperty("bugs").EnumerateArray());
    }

    [Fact(DisplayName = "Чужое рабочее пространство: create_bug в чужой репорт — отказ")]
    public async Task CreateBugForeignWorkspaceIsRejected()
    {
        var owner = ContractScenario.Create(fixture);
        var reportId = await owner.CreateReportAsync();
        var stranger = ContractScenario.Create(fixture);

        await using var client = await Mcp.CreateAsync(stranger);
        var error = await AssertToolFailsAsync(
            client,
            "create_bug",
            Args(("reportId", reportId), ("title", "чужими руками")));

        Assert.False(string.IsNullOrEmpty(error));

        var report = await ContractScenario.ReadJsonAsync(await owner.Client.GetAsync($"/v2/reports/{reportId}"));
        Assert.Empty(report.GetProperty("bugs").EnumerateArray());
    }

    public ValueTask DisposeAsync() => _mcp?.DisposeAsync() ?? ValueTask.CompletedTask;

    private static async Task<JsonElement> CallAsync(
        McpClient client, string tool, IReadOnlyDictionary<string, object?> arguments)
    {
        var result = await client.CallToolAsync(tool, arguments);
        var text = TextOf(result);

        Assert.True(result.IsError != true, $"{tool} вернул ошибку: {text}");

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task<string> AssertToolFailsAsync(
        McpClient client, string tool, IReadOnlyDictionary<string, object?> arguments)
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
