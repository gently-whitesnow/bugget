using System.Text.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Отказ на валидации модели: тот же <c>application/problem+json</c> из общего
/// каталога (ADR-0008) плюс словарь <c>errors</c>, ключи которого — wire-пути полей.
/// </summary>
internal static class ValidationProblemDetailsContract
{
    public static async Task AssertSingleErrorAsync(
        HttpResponseMessage response,
        string expectedKey,
        params string[] expectedMessages)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal("model_state_validation_error", root.GetProperty("code").GetString());
        Assert.Equal("urn:bugget:error:model_state_validation_error", root.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));

        var errors = root.GetProperty("errors");
        var error = Assert.Single(errors.EnumerateObject());
        Assert.Equal(expectedKey, error.Name);
        Assert.Equal(
            expectedMessages,
            error.Value.EnumerateArray().Select(message => message.GetString()).ToArray());
    }
}
