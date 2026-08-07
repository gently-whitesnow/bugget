using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Содержимое вложений через <c>get_attachment</c> (P2d, token-economy):
/// картинки — превью по умолчанию и оригинал только по явному флагу; видео —
/// байты не уходят никогда; текст — как есть, без перекодировок, страницами с
/// явным <c>truncated</c>. Байты сверяются с REST-эндпоинтами того же хоста:
/// MCP не альтернативное хранилище, а другой транспорт к тем же файлам.
/// </summary>
[Collection("PostgresCollection")]
public sealed class McpAttachmentContentContractTests(AppContractFixture fixture)
    : IClassFixture<AppContractFixture>, IAsyncDisposable
{
    private readonly List<HttpClientTransport> _transports = [];

    [Fact(DisplayName = "Картинка по умолчанию: превью, и байты равны REST-превью")]
    public async Task ImageDefaultsToPreviewBytes()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var attachmentId = await scenario.UploadBugAttachmentAsync(reportId, bugId);

        await using var client = await CreateMcpClientAsync(scenario);
        var result = await CallAsync(client, "get_attachment",
            Args(("reportId", reportId), ("attachmentId", attachmentId)));

        var image = Assert.Single(result.Content.OfType<ImageContentBlock>());
        Assert.Equal("image/webp", image.MimeType);

        var restPreview = await scenario.Client.GetByteArrayAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/attachments/{attachmentId}/content/preview");
        Assert.Equal(restPreview, image.DecodedData.ToArray());

        var restOriginal = await scenario.Client.GetByteArrayAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/attachments/{attachmentId}/content");
        Assert.NotEqual(restOriginal, image.DecodedData.ToArray());
    }

    [Fact(DisplayName = "Картинка с original=true: байты равны REST-оригиналу")]
    public async Task ImageOriginalRequiresExplicitFlag()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var attachmentId = await scenario.UploadBugAttachmentAsync(reportId, bugId);

        await using var client = await CreateMcpClientAsync(scenario);
        var result = await CallAsync(client, "get_attachment",
            Args(("reportId", reportId), ("attachmentId", attachmentId), ("original", true)));

        var image = Assert.Single(result.Content.OfType<ImageContentBlock>());
        var restOriginal = await scenario.Client.GetByteArrayAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/attachments/{attachmentId}/content");
        Assert.Equal(restOriginal, image.DecodedData.ToArray());
    }

    [Fact(DisplayName = "Русский текст приходит как есть, символ в символ")]
    public async Task RussianTextArrivesVerbatim()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        const string text = "Ошибка: файл «отчёт.xlsx» не найден.\nПовторите попытку позже — сервис недоступен.";
        var attachmentId = await UploadTextAsync(scenario, reportId, bugId, text);

        await using var client = await CreateMcpClientAsync(scenario);
        var result = await CallAsync(client, "get_attachment",
            Args(("reportId", reportId), ("attachmentId", attachmentId)));

        var (page, body) = TextBlocks(result);
        Assert.Equal(text, body);
        Assert.Equal(text.Length, page.GetProperty("total_chars").GetInt32());
        Assert.False(page.GetProperty("truncated").GetBoolean());

        // Метаданные подтверждают: это text/plain, и модель не платила за конвертацию.
        var meta = MetaOf(result);
        Assert.Equal("text/plain", meta.GetProperty("mime_type").GetString());
    }

    [Fact(DisplayName = "Пагинация текста: страницы склеиваются в исходник, truncated честный")]
    public async Task TextPaginationIsExplicit()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        const string text = "абвгдеёжзи";
        var attachmentId = await UploadTextAsync(scenario, reportId, bugId, text);

        await using var client = await CreateMcpClientAsync(scenario);

        var assembled = new StringBuilder();
        var offset = 0;
        bool truncated;
        do
        {
            var result = await CallAsync(client, "get_attachment",
                Args(("reportId", reportId), ("attachmentId", attachmentId), ("offset", offset), ("maxChars", 4)));
            var (page, body) = TextBlocks(result);

            Assert.Equal(offset, page.GetProperty("offset").GetInt32());
            Assert.Equal(body.Length, page.GetProperty("returned_chars").GetInt32());

            assembled.Append(body);
            offset += body.Length;
            truncated = page.GetProperty("truncated").GetBoolean();
        }
        while (truncated);

        Assert.Equal(text, assembled.ToString());
    }

    [Fact(DisplayName = "Видео: метаданные и ссылка для человека, байтов нет даже по original")]
    public async Task VideoBytesNeverLeaveTheServer()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var attachmentId = await UploadVideoAsync(scenario, reportId, bugId);

        await using var client = await CreateMcpClientAsync(scenario);
        var result = await CallAsync(client, "get_attachment",
            Args(("reportId", reportId), ("attachmentId", attachmentId)));

        var meta = MetaOf(result);
        Assert.Equal("video/mp4", meta.GetProperty("mime_type").GetString());
        Assert.Equal(
            $"/api/app/workspaces/{scenario.WorkspaceId}/teams/{scenario.TeamId}" +
            $"/v2/reports/{reportId}/bugs/{bugId}/attachments/{attachmentId}/content",
            meta.GetProperty("download_path").GetString());

        // Оригинал видео крупнее любого кадра-превью; если картинка и есть, это
        // не байты ролика. Здесь превью не построено (ffmpeg на минимальном mp4
        // не отрабатывает), поэтому image-блоков нет вовсе.
        Assert.Empty(result.Content.OfType<ImageContentBlock>());

        var refusal = await client.CallToolAsync(
            "get_attachment",
            Args(("reportId", reportId), ("attachmentId", attachmentId), ("original", true)));
        Assert.True(refusal.IsError == true);
        Assert.Contains("download_path", TextOf(refusal), StringComparison.Ordinal);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var transport in _transports)
        {
            await transport.DisposeAsync();
        }
    }

    /// <summary>
    /// Текстовое вложение. Тип определяется сервером по содержимому (libmagic),
    /// заголовок клиента — только подсказка.
    /// </summary>
    private static async Task<int> UploadTextAsync(
        ContractScenario scenario, string reportId, int bugId, string text)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        var response = await scenario.Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/attachments?attachType=fact",
            new MultipartFormDataContent { { file, "file", "лог.txt" } });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        return (await ContractScenario.ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    /// <summary>
    /// Минимальный валидный mp4: один ftyp-box, по которому libmagic узнаёт
    /// video/mp4. Дальше ffmpeg на нём превью не построит — и не должен: тест
    /// проверяет именно путь «байты видео не уходят», а не перекодирование.
    /// </summary>
    private static async Task<int> UploadVideoAsync(ContractScenario scenario, string reportId, int bugId)
    {
        var ftyp = new byte[]
        {
            0x00, 0x00, 0x00, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p',
            (byte)'i', (byte)'s', (byte)'o', (byte)'m', 0x00, 0x00, 0x02, 0x00,
            (byte)'i', (byte)'s', (byte)'o', (byte)'m', (byte)'m', (byte)'p', (byte)'4', (byte)'1',
        };
        var file = new ByteArrayContent(ftyp);
        file.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        var response = await scenario.Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/attachments?attachType=fact",
            new MultipartFormDataContent { { file, "file", "repro.mp4" } });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        return (await ContractScenario.ReadJsonAsync(response)).GetProperty("id").GetInt32();
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

    private static async Task<CallToolResult> CallAsync(
        McpClient client, string tool, IReadOnlyDictionary<string, object?> arguments)
    {
        var result = await client.CallToolAsync(tool, arguments);
        Assert.True(result.IsError != true, $"{tool} вернул ошибку: {TextOf(result)}");
        return result;
    }

    /// <summary>Первый текстовый блок — всегда JSON метаданных вложения.</summary>
    private static JsonElement MetaOf(CallToolResult result) =>
        JsonDocument.Parse(result.Content.OfType<TextContentBlock>().First().Text).RootElement.Clone();

    /// <summary>
    /// Для текста блоки идут так: метаданные вложения, страница пагинации,
    /// сырое содержимое.
    /// </summary>
    private static (JsonElement Page, string Body) TextBlocks(CallToolResult result)
    {
        var texts = result.Content.OfType<TextContentBlock>().ToArray();
        Assert.Equal(3, texts.Length);

        return (JsonDocument.Parse(texts[1].Text).RootElement.Clone(), texts[2].Text);
    }

    private static string TextOf(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    private static Dictionary<string, object?> Args(params (string Name, object Value)[] arguments) =>
        arguments.ToDictionary(argument => argument.Name, argument => (object?)argument.Value);
}
