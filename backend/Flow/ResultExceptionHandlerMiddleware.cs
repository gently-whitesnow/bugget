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

            await ProblemDetailsFactory.WriteAsync(
                context,
                InternalServerError.Error,
                InternalServerError.Reason,
                (int)HttpStatusCode.InternalServerError);
        }
    }
}
