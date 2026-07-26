using System.Text.Json;
using Bugget.ExternalClients.Kaiten.Models;

namespace Bugget.ExternalClients.Kaiten;

/// <summary>
/// Клиент для работы с Kaiten API.
/// Использует IHttpClientFactory для управления HttpClient.
/// </summary>
public sealed class KaitenClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;
    private readonly string _accessToken;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public KaitenClient(IHttpClientFactory httpClientFactory, string baseUrl, string accessToken)
    {
        _httpClientFactory = httpClientFactory;
        _baseUrl = baseUrl.TrimEnd('/');
        _accessToken = accessToken;
    }

    public async Task<KaitenCardResponse?> GetCardAsync(string cardId)
    {
        var url = $"{_baseUrl}/api/latest/cards/{cardId}";
        using var client = CreateClient();
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<KaitenCardResponse>(content, JsonOptions);
    }
    public async Task<KaitenCardResponse[]> SearchAsync(string? query, uint offset, uint limit, int boardId)
    {
        var threeMonthAgo = DateTime.UtcNow.AddMonths(-3).ToString("O");
        var url = $"{_baseUrl}/api/latest/cards?query={Uri.EscapeDataString(query ?? "")}&offset={offset}&limit={limit}" +
                  $"&search_fields=title&updated_after={threeMonthAgo}&archived=false&condition=1&board_id={boardId}";

        using var client = CreateClient();
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<KaitenCardResponse[]>(content, JsonOptions) ?? [];
    }

    public async Task<KaitenSpaceResponse[]> GetSpacesAsync()
    {
        var url = $"{_baseUrl}/api/latest/spaces";

        using var client = CreateClient();
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<KaitenSpaceResponse[]>(content, JsonOptions) ?? [];
    }

    /// <summary>
    /// Добавляет внешнюю ссылку к карточке.
    /// </summary>
    public async Task AddExternalLinkAsync(string cardId, string url, string description)
    {
        var requestUrl = $"{_baseUrl}/api/latest/cards/{cardId}/external-links";

        var payload = new { url, description };
        var jsonContent = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var client = CreateClient();
        using var response = await client.PostAsync(requestUrl, jsonContent);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Добавляет комментарий к карточке.
    /// </summary>
    public async Task AddCommentAsync(string cardId, string text)
    {
        var requestUrl = $"{_baseUrl}/api/latest/cards/{cardId}/comments";

        var payload = new { text };
        var jsonContent = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var client = CreateClient();
        using var response = await client.PostAsync(requestUrl, jsonContent);
        response.EnsureSuccessStatusCode();
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");
        return client;
    }
}

