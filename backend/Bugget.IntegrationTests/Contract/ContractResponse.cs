using System.Net;
using System.Text.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>Явные проверки ответа: статус, media type и общий каталог ошибок (ADR-0008).</summary>
/// <remarks>
/// Форму тела целиком здесь не сравнивают: провод описан в <c>specs/contracts/**/openapi.yaml</c>, соответствие
/// держат гейты <c>backend-contracts</c> и <c>frontend-contracts</c>. Тесты проверяют поведение в сценарии.
/// </remarks>
internal static class ContractResponse
{
    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        AssertStatus(expected, response, body);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        return JsonDocument.Parse(body).RootElement.Clone();
    }

    public static async Task EmptyAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        AssertStatus(expected, response, body);
        Assert.Equal(string.Empty, body);
    }

    /// <summary>
    /// Отказ по общему каталогу: <c>application/problem+json</c>, стабильный <c>code</c>, выведенный из него
    /// <c>type</c>, непустые <c>title</c> и <c>traceId</c>. Прикладные поля отказа проверяет вызывающий.
    /// </summary>
    public static async Task<JsonElement> ProblemAsync(
        HttpResponseMessage response,
        string code,
        HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        AssertStatus(expected, response, body);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var root = JsonDocument.Parse(body).RootElement.Clone();

        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.Equal($"urn:bugget:error:{code}", root.GetProperty("type").GetString());
        Assert.Equal((int)expected, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));

        return root;
    }

    private static void AssertStatus(HttpStatusCode expected, HttpResponseMessage response, string body)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        Assert.Fail($"ожидали {(int)expected}, получили {(int)response.StatusCode}: {body}");
    }
}
