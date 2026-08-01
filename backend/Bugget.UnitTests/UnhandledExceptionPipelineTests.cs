using System.Text.Json;
using Bugget.Api.Authorization;
using Bugget.Api.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bugget.UnitTests;

/// <summary>
/// Внешний обработчик необработанных исключений в хосте — один на весь процесс,
/// включая модули users и authorization (у них был свой, дублировавший этот, ADR-0004).
/// Проверяем то же, что раньше проверял тест модуля authorization: наружу уходит
/// problem+json единой формы, текст исключения в тело не попадает.
/// </summary>
public sealed class UnhandledExceptionPipelineTests
{
    [Fact]
    public async Task Unexpected_module_failure_becomes_sanitized_problem_details()
    {
        const string secret = "redis endpoint 10.0.0.8 password=hunter2";
        var context = await InvokeHostPipelineAsync(new InvalidOperationException(secret));

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

    [Fact]
    public async Task KeyNotFound_of_a_module_still_becomes_404()
    {
        var context = await InvokeHostPipelineAsync(new KeyNotFoundException("no such user"));

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

        using var document = JsonDocument.Parse(await ReadBodyAsync(context));
        Assert.Equal("not_found", document.RootElement.GetProperty("code").GetString());
    }

    /// <summary>
    /// Повторяет порядок production pipeline: общий обработчик хоста снаружи,
    /// authorization NotFound-handler внутри него.
    /// </summary>
    private static async Task<DefaultHttpContext> InvokeHostPipelineAsync(Exception exception)
    {
        var authorization = new NotFoundExceptionMiddleware(
            NullLogger<NotFoundExceptionMiddleware>.Instance);
        var serverErrors = new ResultExceptionHandlerMiddleware(
            NullLogger<ResultExceptionHandlerMiddleware>.Instance);
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
