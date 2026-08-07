using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Read-инструменты MCP: <c>list_reports</c>, <c>get_report</c>,
/// <c>search_reports</c>, <c>get_attachment</c>.
///
/// Данные заводятся через публичный REST того же хоста, а читаются настоящим
/// MCP-клиентом по JSON-RPC: инструменты обязаны видеть ровно то, что видит по
/// этой identity фронт, и ничего сверх. Форма ответа своя, компактная, но строки
/// enum'ов — общие с REST, поэтому там, где важно именно совпадение, тест
/// сравнивает два ответа между собой, а не с зашитым литералом.
/// </summary>
[Collection("PostgresCollection")]
public sealed class McpReadToolsContractTests(AppContractFixture fixture)
    : IClassFixture<AppContractFixture>, IAsyncDisposable
{
    private readonly List<HttpClientTransport> _transports = [];

    [Fact(DisplayName = "tools/list: все четыре read-инструмента объявлены")]
    public async Task ServerAdvertisesFourReadTools()
    {
        var scenario = ContractScenario.Create(fixture);
        await using var client = await CreateMcpClientAsync(scenario);

        var tools = (await client.ListToolsAsync()).Select(tool => tool.Name).ToArray();

        // Точный список всей поверхности (read + write) держит
        // McpWriteToolsContractTests: два точных списка расходились бы при каждом
        // добавлении инструмента. Здесь — что read-четвёрка на месте.
        Assert.Superset(
            new HashSet<string> { "get_attachment", "get_report", "list_reports", "search_reports" },
            tools.ToHashSet());
    }

    [Fact(DisplayName = "list_reports: репорт рабочего пространства, статус строкой, без дерева багов")]
    public async Task ListReportsReturnsCompactWorkspaceReports()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync("mcp-список");
        await scenario.CreateBugAsync(reportId);

        await using var client = await CreateMcpClientAsync(scenario);
        var page = await CallAsync(client, "list_reports", Args(("take", 100)));
        var report = FindReport(page, reportId);

        Assert.Equal("mcp-список", report.GetProperty("title").GetString());
        Assert.Equal("backlog", report.GetProperty("status").GetString());
        Assert.Equal("user", report.GetProperty("creator_type").GetString());
        Assert.Equal(1, report.GetProperty("bugs_count").GetInt32());

        // Компактность — часть контракта инструмента, а не деталь реализации:
        // содержимое багов и поля аналитики в списке не едут.
        Assert.False(report.TryGetProperty("bugs", out _));
        Assert.False(report.TryGetProperty("is_excluded_from_analytics", out _));
    }

    [Fact(DisplayName = "get_report: дерево репорта целиком, вложения — только метаданными")]
    public async Task GetReportReturnsWholeTree()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var commentId = await scenario.CreateCommentAsync(reportId, bugId);
        await scenario.CreateStepAsync(reportId, bugId);
        var attachmentId = await scenario.UploadBugAttachmentAsync(reportId, bugId);
        await scenario.UploadCommentAttachmentAsync(reportId, bugId, commentId);

        await using var client = await CreateMcpClientAsync(scenario);
        var report = await CallAsync(client, "get_report", Args(("reportId", reportId)));
        var bug = Single(report, "bugs");

        Assert.Equal(reportId, report.GetProperty("id").GetString());
        Assert.Equal(bugId, bug.GetProperty("id").GetInt32());
        Assert.Equal("получили это", bug.GetProperty("receive").GetString());
        Assert.Equal(commentId, Single(bug, "comments").GetProperty("id").GetInt32());
        Assert.Single(bug.GetProperty("steps").EnumerateArray());

        var attachment = Single(bug, "attachments");
        Assert.Equal(attachmentId, attachment.GetProperty("id").GetInt32());

        // Ключ хранилища, mime и размер REST наружу не отдаёт — MCP не место, где
        // это решение отменяется мимоходом.
        Assert.False(attachment.TryGetProperty("mime_type", out _));
        Assert.False(attachment.TryGetProperty("storage_key", out _));
        Assert.False(attachment.TryGetProperty("length_bytes", out _));
    }

    [Fact(DisplayName = "get_report: enum'ы приходят теми же строками, что и в ответе REST")]
    public async Task GetReportUsesSameEnumStringsAsRest()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        await scenario.CreateCommentAsync(reportId, bugId);
        await scenario.UploadBugAttachmentAsync(reportId, bugId, attachType: "expected");

        var rest = await ContractScenario.ReadJsonAsync(await scenario.Client.GetAsync($"/v2/reports/{reportId}"));
        var restBug = Single(rest, "bugs");

        await using var client = await CreateMcpClientAsync(scenario);
        var mcp = await CallAsync(client, "get_report", Args(("reportId", reportId)));
        var mcpBug = Single(mcp, "bugs");

        AssertSameString(rest, mcp, "status");
        AssertSameString(rest, mcp, "creator_type");
        AssertSameString(restBug, mcpBug, "status");
        AssertSameString(Single(restBug, "comments"), Single(mcpBug, "comments"), "audience");
        AssertSameString(Single(restBug, "attachments"), Single(mcpBug, "attachments"), "attach_type");
    }

    [Fact(DisplayName = "search_reports: находит репорт по слову из заголовка")]
    public async Task SearchReportsFindsReportByQuery()
    {
        var scenario = ContractScenario.Create(fixture);
        var marker = $"мцпмаркер{Random.Shared.Next(100_000, 999_999)}";
        var reportId = await scenario.CreateReportAsync($"репорт {marker}");

        await using var client = await CreateMcpClientAsync(scenario);
        var page = await CallAsync(client, "search_reports", Args(("query", marker)));

        Assert.Equal(reportId, FindReport(page, reportId).GetProperty("id").GetString());
    }

    [Fact(DisplayName = "get_attachment: первый блок — метаданные без ключа хранилища")]
    public async Task GetAttachmentReturnsMetadataFirst()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var attachmentId = await scenario.UploadBugAttachmentAsync(reportId, bugId, "снимок.png");

        await using var client = await CreateMcpClientAsync(scenario);
        var result = await client.CallToolAsync(
            "get_attachment",
            Args(("reportId", reportId), ("attachmentId", attachmentId)));
        Assert.True(result.IsError != true, TextOf(result));

        // Содержимое (P2d) едет отдельными блоками — его контракт держит
        // McpAttachmentContentContractTests. Здесь — форма метаданных.
        var attachment = JsonDocument.Parse(
            result.Content.OfType<TextContentBlock>().First().Text).RootElement;

        Assert.Equal(attachmentId, attachment.GetProperty("id").GetInt32());
        Assert.Equal(reportId, attachment.GetProperty("report_id").GetString());
        Assert.Equal(bugId, attachment.GetProperty("entity_id").GetInt32());
        // Картинки при загрузке нормализуются в webp — имя на диске уже не .png.
        Assert.Equal("снимок.webp", attachment.GetProperty("file_name").GetString());
        Assert.Equal("fact", attachment.GetProperty("attach_type").GetString());
        Assert.False(attachment.TryGetProperty("storage_key", out _));
    }

    [Fact(DisplayName = "Чужое рабочее пространство: репорта нет ни в списке, ни в поиске, ни по идентификатору")]
    public async Task ForeignWorkspaceStaysInvisible()
    {
        var owner = ContractScenario.Create(fixture);
        var marker = $"чужоймаркер{Random.Shared.Next(100_000, 999_999)}";
        var foreignReportId = await owner.CreateReportAsync($"репорт {marker}");
        var bugId = await owner.CreateBugAsync(foreignReportId);
        var attachmentId = await owner.UploadBugAttachmentAsync(foreignReportId, bugId);

        await using var client = await CreateMcpClientAsync(ContractScenario.Create(fixture));

        Assert.DoesNotContain(foreignReportId, ReportIds(await CallAsync(client, "list_reports", Args(("take", 100)))));
        Assert.DoesNotContain(foreignReportId, ReportIds(await CallAsync(client, "search_reports", Args(("query", marker)))));
        await AssertToolFailsAsync(client, "get_report", Args(("reportId", foreignReportId)));
        await AssertToolFailsAsync(
            client,
            "get_attachment",
            Args(("reportId", foreignReportId), ("attachmentId", attachmentId)));
    }

    [Fact(DisplayName = "Чужая команда того же пространства: репорт не читается по идентификатору")]
    public async Task ForeignTeamReportIsNotReadable()
    {
        var owner = ContractScenario.Create(fixture);
        var foreignReportId = await owner.CreateReportAsync();

        // Тот же workspace, другая команда: разрешение идентификатора обязано
        // упереться в creator_team_id, как и на REST-ручке репорта.
        await using var client = await CreateMcpClientAsync(
            owner.WorkspaceId,
            $"{owner.TeamId}9",
            $"user-{Guid.NewGuid():N}");

        await AssertToolFailsAsync(client, "get_report", Args(("reportId", foreignReportId)));
    }

    [Fact(DisplayName = "Неизвестное значение фильтра: отказ с перечислением допустимых")]
    public async Task UnknownStatusIsRejectedWithAllowedValues()
    {
        var scenario = ContractScenario.Create(fixture);
        await using var client = await CreateMcpClientAsync(scenario);

        var text = await AssertToolFailsAsync(
            client,
            "list_reports",
            Args(("reportStatuses", new[] { "почти-backlog" })));

        // Сообщение должно подсказать модели допустимые значения, а не просто
        // сказать «плохо» — иначе следующий вызов будет тем же самым.
        Assert.Contains("backlog", text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Страница шире потолка: отказ, а не молча урезанная выдача")]
    public async Task TakeAboveLimitIsRejected()
    {
        var scenario = ContractScenario.Create(fixture);
        await using var client = await CreateMcpClientAsync(scenario);

        await AssertToolFailsAsync(client, "list_reports", Args(("take", 500)));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var transport in _transports)
        {
            await transport.DisposeAsync();
        }
    }

    private Task<McpClient> CreateMcpClientAsync(ContractScenario scenario) =>
        CreateMcpClientAsync(scenario.WorkspaceId, scenario.TeamId, scenario.UserId);

    /// <summary>
    /// Клиент MCP с identity-заголовками ровно того вида, что проставляет nginx
    /// после успешного <c>auth_request</c>. Сам обмен PAT на заголовки проверяет
    /// <see cref="McpEndpointContractTests"/>, здесь важен не он, а то, что видно
    /// под этой identity.
    /// </summary>
    private async Task<McpClient> CreateMcpClientAsync(string workspaceId, string teamId, string userId)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(fixture.BaseAddress, "/v1/mcp"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    [ContractHeaders.UserId] = userId,
                    [ContractHeaders.TeamId] = teamId,
                    [ContractHeaders.WorkspaceId] = workspaceId,
                    [ContractHeaders.WorkspaceRole] = "owner",
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

        // IsError — bool?: на успехе сервер поле опускает (null), на отказе ставит
        // true. Assert.False(null) в xUnit падает, хотя это как раз happy path.
        Assert.True(
            result.IsError != true,
            $"{tool} вернул ошибку: {text}");

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task<string> AssertToolFailsAsync(
        McpClient client,
        string tool,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var result = await client.CallToolAsync(tool, arguments);
        var text = TextOf(result);

        Assert.True(
            result.IsError == true,
            $"{tool} обязан был отказать, но ответил: {text}");

        // Только флага мало: успех с опущенным isError (null) и отказ без
        // флага выглядели бы одинаково. Текст отказа — сообщение, не JSON
        // ответа инструмента.
        Assert.False(
            LooksLikeJsonObject(text),
            $"{tool} помечен ошибкой, но в content лежит JSON-ответ: {text}");

        return text;
    }

    private static bool LooksLikeJsonObject(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string TextOf(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    private static Dictionary<string, object?> Args(params (string Name, object Value)[] arguments) =>
        arguments.ToDictionary(argument => argument.Name, argument => (object?)argument.Value);

    private static JsonElement FindReport(JsonElement page, string reportId) =>
        page.GetProperty("reports")
            .EnumerateArray()
            .Single(report => report.GetProperty("id").GetString() == reportId);

    private static string[] ReportIds(JsonElement page) =>
        [.. page.GetProperty("reports").EnumerateArray().Select(report => report.GetProperty("id").GetString()!)];

    private static JsonElement Single(JsonElement parent, string property) =>
        parent.GetProperty(property).EnumerateArray().Single();

    private static void AssertSameString(JsonElement rest, JsonElement mcp, string property) =>
        Assert.Equal(rest.GetProperty(property).GetString(), mcp.GetProperty(property).GetString());
}
