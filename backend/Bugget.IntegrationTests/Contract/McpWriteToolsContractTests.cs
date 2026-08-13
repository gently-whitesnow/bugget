using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Write-инструменты MCP: <c>create_report</c>, <c>create_bug</c>,
/// <c>patch_report</c>, <c>patch_bug</c>, <c>create_comment</c>,
/// <c>update_comment</c>.
///
/// Клиент здесь ходит с identity, в которой способ входа — PAT
/// (<c>Auth-Request-Auth-Method: pat</c>, ровно как выставляет
/// <c>/_internal/auth</c>): записи обязаны ложиться в историю как действия
/// агента, а не человека-владельца токена. Изоляция данных — та же, что у REST:
/// её держат сервисы, и тесты проверяют, что write-инструменты её не ослабили.
/// </summary>
[Collection("PostgresCollection")]
public sealed class McpWriteToolsContractTests(AppContractFixture fixture)
    : IClassFixture<AppContractFixture>, IAsyncDisposable
{
    private readonly List<HttpClientTransport> _transports = [];

    [Fact(DisplayName = "tools/list: ровно четыре read- и четыре write-инструмента, запрещённых нет")]
    public async Task ServerAdvertisesExactlyExpectedTools()
    {
        var scenario = ContractScenario.Create(fixture);
        await using var client = await CreateMcpClientAsync(scenario);

        var tools = (await client.ListToolsAsync()).Select(tool => tool.Name).Order().ToArray();

        // Точный список вместо Contains: шаги, аналитика и настройки не должны
        // появиться незамеченными — это граница поверхности, а не случайность.
        Assert.Equal(
            [
                "create_bug", "create_comment", "create_report", "get_attachment",
                "get_report", "list_reports", "patch_bug", "patch_report",
                "search_reports", "update_comment",
            ],
            tools);
    }

    [Fact(DisplayName = "patch_report: статус меняется и виден и в MCP, и в REST")]
    public async Task PatchReportChangesStatus()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync("mcp-патч-статуса");

        await using var client = await CreateMcpClientAsync(scenario);
        var patched = await CallAsync(
            client,
            "patch_report",
            Args(("reportId", reportId), ("status", "fix")));

        Assert.Equal(reportId, patched.GetProperty("id").GetString());
        Assert.Equal("fix", patched.GetProperty("status").GetString());

        // Правка через MCP — это правка того же репорта, который видит фронт.
        var rest = await ContractScenario.ReadJsonAsync(await scenario.Client.GetAsync($"/v2/reports/{reportId}"));
        Assert.Equal("fix", rest.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "patch_report: агент забирает репорт в fix и возвращает тестировщику в test")]
    public async Task PatchReportHandsReportBetweenAgentOwnerAndTester()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync("mcp-передача-ответственности");

        // Владелец PAT — не автор репорта: иначе передача была бы неотличима от no-op.
        var ownerUserId = $"pat-owner-{Guid.NewGuid():N}";
        await using var client = await CreateMcpClientAsync(scenario, ownerUserId);

        // Агент начал правки: репорт в fix, держит его владелец токена.
        var inFix = await CallAsync(
            client,
            "patch_report",
            Args(("reportId", reportId), ("status", "fix")));

        Assert.Equal("fix", inFix.GetProperty("status").GetString());
        Assert.Equal(ownerUserId, inFix.GetProperty("responsible_user_id").GetString());

        // Агент запушил: репорт в test, ответственность возвращается тестировщику —
        // тому, кто держал репорт до агента (автору-создателю сценария).
        var inTest = await CallAsync(
            client,
            "patch_report",
            Args(("reportId", reportId), ("status", "test")));

        Assert.Equal("test", inTest.GetProperty("status").GetString());
        Assert.Equal(scenario.UserId, inTest.GetProperty("responsible_user_id").GetString());
    }

    [Fact(DisplayName = "patch_bug: текст и статус меняются, непереданные поля остаются")]
    public async Task PatchBugChangesTextAndStatus()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        await using var client = await CreateMcpClientAsync(scenario);
        var patched = await CallAsync(
            client,
            "patch_bug",
            Args(
                ("reportId", reportId),
                ("bugId", bugId),
                ("status", "fixed"),
                ("receive", "после фикса получаем ожидаемое")));

        Assert.Equal(bugId, patched.GetProperty("id").GetInt32());
        Assert.Equal("fixed", patched.GetProperty("status").GetString());
        Assert.Equal("после фикса получаем ожидаемое", patched.GetProperty("receive").GetString());
        // Expect не передавали — значение из ContractScenario.CreateBugAsync на месте.
        Assert.Equal("ожидали то", patched.GetProperty("expect").GetString());
    }

    [Fact(DisplayName = "create_comment: запись по PAT-identity видна в истории как действие агента")]
    public async Task CreateCommentIsAttributedToAgent()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        await using var client = await CreateMcpClientAsync(scenario);
        var comment = await CallAsync(
            client,
            "create_comment",
            Args(("reportId", reportId), ("bugId", bugId), ("text", "починено агентом")));

        Assert.Equal("починено агентом", comment.GetProperty("text").GetString());
        Assert.Equal("agent", comment.GetProperty("creator_type").GetString());
        Assert.Equal(scenario.UserId, comment.GetProperty("creator_user_id").GetString());
        Assert.Equal("internal", comment.GetProperty("audience").GetString());

        // И в дереве репорта — там, где историю читают люди и модель — тоже agent,
        // рядом с обычным человеческим комментарием это различимо.
        var restCommentId = await scenario.CreateCommentAsync(reportId, bugId);
        var report = await CallAsync(client, "get_report", Args(("reportId", reportId)));
        var comments = Single(report, "bugs").GetProperty("comments").EnumerateArray().ToArray();

        Assert.Equal(
            "agent",
            comments.Single(c => c.GetProperty("id").GetInt32() == comment.GetProperty("id").GetInt32())
                .GetProperty("creator_type").GetString());
        Assert.Equal(
            "user",
            comments.Single(c => c.GetProperty("id").GetInt32() == restCommentId)
                .GetProperty("creator_type").GetString());
    }

    [Fact(DisplayName = "patch_report по PAT-identity: событие статуса записано за агентом")]
    public async Task PatchReportEmitsAgentActor()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        await using var client = await CreateMcpClientAsync(scenario);
        await CallAsync(client, "patch_report", Args(("reportId", reportId), ("status", "fix")));

        // История статусов живёт в domain_events; actor_creator_type там — уже
        // числовое значение домена: CreatorType.Agent = 3. Это чтение служебной
        // таблицы для проверки атрибуции, а не подготовка данных — данные выше
        // созданы только публичным API. Фильтр по актору: user id сценария уникален
        // на каждый тест, поэтому событие гарантированно наше, а не соседнего теста.
        await using var connection = new Npgsql.NpgsqlConnection(
            Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING"));
        await connection.OpenAsync();
        var actorType = await Dapper.SqlMapper.QuerySingleAsync<short?>(
            connection,
            """
            SELECT actor_creator_type
            FROM domain_events
            WHERE event_type = 'bugget.report.status_changed' AND actor_user_id = @actorUserId
            """,
            new { actorUserId = scenario.UserId });
        Assert.Equal((short)3, actorType);
    }

    [Fact(DisplayName = "update_comment: текст обновляется")]
    public async Task UpdateCommentChangesText()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        await using var client = await CreateMcpClientAsync(scenario);
        var created = await CallAsync(
            client,
            "create_comment",
            Args(("reportId", reportId), ("bugId", bugId), ("text", "первая версия")));

        var updated = await CallAsync(
            client,
            "update_comment",
            Args(
                ("reportId", reportId),
                ("bugId", bugId),
                ("commentId", created.GetProperty("id").GetInt32()),
                ("text", "вторая версия")));

        Assert.Equal(created.GetProperty("id").GetInt32(), updated.GetProperty("id").GetInt32());
        Assert.Equal("вторая версия", updated.GetProperty("text").GetString());
    }

    [Fact(DisplayName = "Чужое рабочее пространство: ни один write-инструмент не достаёт до репорта")]
    public async Task ForeignWorkspaceIsNotWritable()
    {
        var owner = ContractScenario.Create(fixture);
        var foreignReportId = await owner.CreateReportAsync();
        var foreignBugId = await owner.CreateBugAsync(foreignReportId);
        var foreignCommentId = await owner.CreateCommentAsync(foreignReportId, foreignBugId);

        await using var client = await CreateMcpClientAsync(ContractScenario.Create(fixture));

        await AssertToolFailsAsync(client, "patch_report", Args(("reportId", foreignReportId), ("status", "fix")));
        await AssertToolFailsAsync(
            client,
            "patch_bug",
            Args(("reportId", foreignReportId), ("bugId", foreignBugId), ("status", "fixed")));
        await AssertToolFailsAsync(
            client,
            "create_comment",
            Args(("reportId", foreignReportId), ("bugId", foreignBugId), ("text", "не должно записаться")));
        await AssertToolFailsAsync(
            client,
            "update_comment",
            Args(
                ("reportId", foreignReportId),
                ("bugId", foreignBugId),
                ("commentId", foreignCommentId),
                ("text", "не должно записаться")));

        // Чужой репорт не изменился: статус исходный, комментарий один и с исходным текстом.
        var rest = await ContractScenario.ReadJsonAsync(await owner.Client.GetAsync($"/v2/reports/{foreignReportId}"));
        Assert.Equal("backlog", rest.GetProperty("status").GetString());
        var comment = Single(Single(rest, "bugs"), "comments");
        Assert.NotEqual("не должно записаться", comment.GetProperty("text").GetString());
    }

    [Fact(DisplayName = "Неизвестный статус: отказ с перечислением допустимых")]
    public async Task UnknownStatusIsRejectedWithAllowedValues()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        await using var client = await CreateMcpClientAsync(scenario);
        var text = await AssertToolFailsAsync(
            client,
            "patch_report",
            Args(("reportId", reportId), ("status", "готово")));

        Assert.Contains("backlog", text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "patch_bug без единого поля: отказ, а не тихий no-op")]
    public async Task EmptyBugPatchIsRejected()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        await using var client = await CreateMcpClientAsync(scenario);
        var text = await AssertToolFailsAsync(
            client,
            "patch_bug",
            Args(("reportId", reportId), ("bugId", bugId)));

        // Отказ должен подсказать модели, что передать, а не просто отказать.
        Assert.Contains("status", text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Текст длиннее лимита REST: отказ, а не запись")]
    public async Task OversizedTextIsRejected()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        await using var client = await CreateMcpClientAsync(scenario);
        await AssertToolFailsAsync(
            client,
            "create_comment",
            Args(("reportId", reportId), ("bugId", bugId), ("text", new string('а', 2049))));
        await AssertToolFailsAsync(
            client,
            "patch_bug",
            Args(("reportId", reportId), ("bugId", bugId), ("title", new string('б', 129))));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var transport in _transports)
        {
            await transport.DisposeAsync();
        }
    }

    /// <summary>
    /// Identity того вида, что выставляет <c>/_internal/auth</c> после входа по
    /// PAT — включая <c>Auth-Request-Auth-Method: pat</c>. Сам обмен PAT на
    /// заголовки проверяет <see cref="McpEndpointContractTests"/>; здесь важно,
    /// что write-запись под такой identity атрибутируется агенту.
    /// </summary>
    private async Task<McpClient> CreateMcpClientAsync(ContractScenario scenario, string? userId = null)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(fixture.BaseAddress, "/v1/mcp"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    [ContractHeaders.UserId] = userId ?? scenario.UserId,
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
