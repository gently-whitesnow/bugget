using System.Text.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

internal static class ValidationProblemDetailsContract
{
    public static async Task AssertSingleErrorAsync(
        HttpResponseMessage response,
        string expectedKey,
        params string[] expectedMessages)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal("model_state_validation_error", root.GetProperty("code").GetString());
        var errors = root.GetProperty("errors");
        var error = Assert.Single(errors.EnumerateObject());
        Assert.Equal(expectedKey, error.Name);
        Assert.Equal(
            expectedMessages,
            error.Value.EnumerateArray().Select(message => message.GetString()).ToArray());
    }
}
