using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Данные для одного contract-теста, созданные только через публичный API.
/// Прямых INSERT'ов здесь нет намеренно: если сломается создание репорта, тесты
/// формы ответа обязаны падать вместе с ним, а не работать на подложенных строках.
/// </summary>
internal sealed class ContractScenario
{
    private ContractScenario(HttpClient client, string workspaceId, string teamId, string userId)
    {
        Client = client;
        WorkspaceId = workspaceId;
        TeamId = teamId;
        UserId = userId;
    }

    public HttpClient Client { get; }

    public string WorkspaceId { get; }

    public string TeamId { get; }

    public string UserId { get; }

    public static ContractScenario Create(AppContractFixture fixture)
    {
        // Идентификаторы workspace/team — числа: nginx достаёт их из пути регуляркой
        // `workspaces/(\d+)` и `teams/(\d+)`, других значений в боевом контуре не бывает.
        var seed = Random.Shared.Next(100_000, 999_999);
        var workspaceId = seed.ToString(CultureInfo.InvariantCulture);
        var teamId = (seed + 1).ToString(CultureInfo.InvariantCulture);
        var userId = $"user-{Guid.NewGuid():N}";

        return new ContractScenario(
            fixture.CreateAuthorizedClient(workspaceId, teamId, userId),
            workspaceId,
            teamId,
            userId);
    }

    public async Task<string> CreateReportAsync(string title = "contract-report")
    {
        var response = await Client.PostAsJsonAsync("/v2/reports", new { title });
        await AssertSuccessAsync(response, "POST /v2/reports");

        return (await ReadJsonAsync(response)).GetProperty("id").GetString()!;
    }

    public async Task<int> CreateBugAsync(string reportId)
    {
        var response = await Client.PostAsJsonAsync(
            $"/v2/reports/{reportId}/bugs",
            new { title = "contract-bug", receive = "получили это", expect = "ожидали то" });
        await AssertSuccessAsync(response, "POST /v2/reports/{aliasId}/bugs");

        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    /// <summary>
    /// Баг, у которого заполнено ровно одно поле из пары <c>receive</c>/<c>expect</c>
    /// и не заполнен <c>title</c>. Бизнес-правило требует хотя бы одно из двух, а не оба,
    /// поэтому такой баг — законный, и наружу он уходит с <c>null</c> во втором ключе.
    /// </summary>
    public async Task<int> CreateOneFieldBugAsync(string reportId, string? receive = null, string? expect = null)
    {
        var response = await Client.PostAsJsonAsync(
            $"/v2/reports/{reportId}/bugs",
            new { receive, expect });
        await AssertSuccessAsync(response, "POST /v2/reports/{aliasId}/bugs (одно поле)");

        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    public async Task<int> CreateLinkAsync(
        string reportId,
        string link = "https://example.test/issue/1",
        string name = "внешняя задача")
    {
        var response = await Client.PostAsJsonAsync($"/v2/reports/{reportId}/links", new { link, name });
        await AssertSuccessAsync(response, "POST /v2/reports/{aliasId}/links");

        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    /// <summary>
    /// Вложение бага. <c>attachType</c> здесь — параметр запроса, и он же уходит
    /// на провод: <c>fact</c> — факт (<c>receive</c>), <c>expected</c> — ожидаемый
    /// результат. Оба значения законны и попадают в один и тот же
    /// <c>bugs[].attachments</c>.
    /// </summary>
    public async Task<int> UploadBugAttachmentAsync(
        string reportId,
        int bugId,
        string fileName = "shot.png",
        string attachType = "fact")
    {
        var response = await Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/attachments?attachType={attachType}",
            FileContent(fileName));
        await AssertSuccessAsync(response, "POST .../bugs/{bugId}/attachments");

        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    /// <summary>
    /// Вложение комментария. Тип вложения здесь не параметр запроса: сервер сам ставит
    /// <c>attach_type = 2</c>, а <c>entity_id</c> — идентификатор комментария.
    /// </summary>
    public async Task<int> UploadCommentAttachmentAsync(
        string reportId,
        int bugId,
        int commentId,
        string fileName = "comment.png")
    {
        var response = await Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/comments/{commentId}/attachments",
            FileContent(fileName));
        await AssertSuccessAsync(response, "POST .../comments/{commentId}/attachments");

        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    /// <summary>
    /// Вложение шага. Сервер ставит <c>attach_type = 3</c>, <c>entity_id</c> — идентификатор шага.
    /// </summary>
    public async Task<int> UploadBugStepAttachmentAsync(
        string reportId,
        int bugId,
        int stepId,
        string fileName = "step.png")
    {
        var response = await Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/steps/{stepId}/attachments",
            FileContent(fileName));
        await AssertSuccessAsync(response, "POST .../steps/{stepId}/attachments");

        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    public async Task<int> CreateCommentAsync(string reportId, int bugId)
    {
        var response = await Client.PostAsJsonAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/comments",
            new { text = "contract-comment" });
        await AssertSuccessAsync(response, "POST .../comments");

        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    public async Task<int> CreateStepAsync(string reportId, int bugId, string text = "contract-step")
    {
        var response = await Client.PostAsJsonAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/steps",
            new { text });
        await AssertSuccessAsync(response, "POST .../steps");

        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    public static MultipartFormDataContent FileContent(string fileName, string contentType = "image/png")
    {
        // Минимальный валидный PNG 1x1 — превью строится картинкой, а не любым байтом.
        var bytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private static async Task AssertSuccessAsync(HttpResponseMessage response, string what)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        Assert.Fail($"{what} вернул {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
}
