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
        var middleware = new NotFoundExceptionMiddleware(NullLogger<NotFoundExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, _ => throw new KeyNotFoundException(secret));

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(body);
        Assert.Equal("not_found", document.RootElement.GetProperty("code").GetString());
        Assert.False(document.RootElement.TryGetProperty("error", out _));
    }
}
