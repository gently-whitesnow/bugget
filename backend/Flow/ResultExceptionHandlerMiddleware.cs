using System;
using System.Net;
using System.Threading.Tasks;
using Flow.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Flow;

public class ResultExceptionHandlerMiddleware(ILogger<ResultExceptionHandlerMiddleware> logger) : IMiddleware
{
    private InternalServerError InternalServerError = new("internal_server_error", "Internal server error");

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Обработана ошибка на мидлваре");

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsJsonAsync(InternalServerError);
        }
    }
}
