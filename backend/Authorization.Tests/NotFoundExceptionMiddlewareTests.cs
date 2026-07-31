using System.Text.Json;
using Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Authorization.Tests;

/// <summary>
/// Граница модуля authorization отдаёт ту же форму, что и весь остальной HTTP-контур,
/// и не публикует текст исключения: до MAIN-69 здесь уходило <c>{ error: ex.Message }</c>.
///
/// Внешний обработчик «любое исключение -> 500» живёт в хосте, и его половину проверяет
/// <c>Bugget.Tests.UnhandledExceptionPipelineTests</c>: сюда он не дотягивается, потому что
/// хост ссылается на этот модуль, а не наоборот.
/// </summary>
public sealed class NotFoundExceptionMiddlewareTests
{
    [Fact]
    public async Task KeyNotFound_becomes_problem_details_without_the_exception_message()
    {
        const string secret = "connection string to the internal database";
        var context = await InvokeAsync(new KeyNotFoundException(secret));

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);

        var body = await ReadBodyAsync(context);
        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(body);
        Assert.Equal("not_found", document.RootElement.GetProperty("code").GetString());
        Assert.False(document.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task Unexpected_failure_is_left_to_the_host_handler()
    {
        var boom = new InvalidOperationException("redis endpoint 10.0.0.8 password=hunter2");

        // Middleware ловит только KeyNotFoundException: всё остальное обязано уйти наверх,
        // иначе в хосте окажется второй обработчик 500 со своей формой ответа.
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeAsync(boom));

        Assert.Same(boom, thrown);
    }

    private static async Task<DefaultHttpContext> InvokeAsync(Exception exception)
    {
        var authorization = new NotFoundExceptionMiddleware(
            NullLogger<NotFoundExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await authorization.InvokeAsync(context, _ => throw exception);

        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }
}
