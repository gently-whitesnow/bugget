using System.Text.Json;
using Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Authorization.Tests;

/// <summary>
/// Граница модуля authorization отдаёт ту же форму, что и весь остальной HTTP-контур,
/// и не публикует текст исключения: до MAIN-69 здесь уходило <c>{ error: ex.Message }</c>.
/// </summary>
public sealed class NotFoundExceptionMiddlewareTests
{
    [Fact]
    public async Task KeyNotFound_becomes_problem_details_without_the_exception_message()
    {
        const string secret = "connection string to the internal database";
        var context = await InvokeAuthorizationPipelineAsync(new KeyNotFoundException(secret));

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);

        var body = await ReadBodyAsync(context);
        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(body);
        Assert.Equal("not_found", document.RootElement.GetProperty("code").GetString());
        Assert.False(document.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task Unexpected_authorization_failure_becomes_sanitized_problem_details()
    {
        const string secret = "redis endpoint 10.0.0.8 password=hunter2";
        var context = await InvokeAuthorizationPipelineAsync(new InvalidOperationException(secret));

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);

        var body = await ReadBodyAsync(context);
        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("internal_server_error", root.GetProperty("code").GetString());
        Assert.Equal("urn:bugget:error:internal_server_error", root.GetProperty("type").GetString());
        Assert.False(root.TryGetProperty("detail", out _));
        Assert.False(root.TryGetProperty("error", out _));
    }

    /// <summary>
    /// Повторяет порядок production pipeline: общий Flow-handler снаружи,
    /// authorization NotFound-handler внутри него.
    /// </summary>
    private static async Task<DefaultHttpContext> InvokeAuthorizationPipelineAsync(Exception exception)
    {
        var authorization = new NotFoundExceptionMiddleware(
            NullLogger<NotFoundExceptionMiddleware>.Instance);
        var serverErrors = new Flow.ResultExceptionHandlerMiddleware(
            NullLogger<Flow.ResultExceptionHandlerMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await serverErrors.InvokeAsync(
            context,
            inner => authorization.InvokeAsync(inner, _ => throw exception));

        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }
}
