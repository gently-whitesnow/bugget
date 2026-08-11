using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Инструменты создания через MCP: <c>create_report</c> и <c>create_bug</c>. Их
/// добавили после MVP (kaiten 237700), чтобы найденный баг заводился тем же PAT,
/// что и правки, а не выходом в неавторизованный CLI скилла. Клиент ходит с
/// PAT-identity (<c>Auth-Request-Auth-Method: pat</c>): созданное обязано ложиться
/// в историю как действие агента, а изоляция workspace/team — не слабее REST.
/// </summary>
[Collection("PostgresCollection")]
public sealed class McpCreateToolsContractTests(AppContractFixture fixture)
    : IClassFixture<AppContractFixture>, IAsyncDisposable
{
    private readonly List<HttpClientTransport> _transports = [];

    [Fact(DisplayName = "create_report: репорт заведён по PAT и виден фронту как действие агента")]
    public async Task CreateReportIsAttributedToAgent()
    {
        var scenario = ContractScenario.Create(fixture);

        await using var client = await CreateMcpClientAsync(scenario);
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

        await using var client = await CreateMcpClientAsync(scenario);
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

        await using var client = await CreateMcpClientAsync(scenario);
        var error = await AssertToolFailsAsync(client, "create_bug", Args(("reportId", reportId)));

        Assert.Contains("хотя бы одно поле", error, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Чужое рабочее пространство: create_bug в чужой репорт — отказ")]
    public async Task CreateBugForeignWorkspaceIsRejected()
    {
        var owner = ContractScenario.Create(fixture);
        var reportId = await owner.CreateReportAsync();
        var stranger = ContractScenario.Create(fixture);

        await using var client = await CreateMcpClientAsync(stranger);
        var error = await AssertToolFailsAsync(
            client,
            "create_bug",
            Args(("reportId", reportId), ("title", "чужими руками")));

        Assert.False(string.IsNullOrEmpty(error));

        // Репорт владельца не пополнился.
        var report = await ContractScenario.ReadJsonAsync(await owner.Client.GetAsync($"/v2/reports/{reportId}"));
        Assert.Empty(report.GetProperty("bugs").EnumerateArray());
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var transport in _transports)
        {
            await transport.DisposeAsync();
        }
    }

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
