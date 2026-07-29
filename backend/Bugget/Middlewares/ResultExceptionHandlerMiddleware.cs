using System.Net;
using Bugget.BO.Errors;
using Flow;
using Monade;
using Monade.Errors;
using Npgsql;

namespace Bugget.Middlewares;

public class ResultExceptionHandlerMiddleware(ILogger<ResultExceptionHandlerMiddleware> logger) : IMiddleware
{
    private readonly ILogger _logger = logger;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (PostgresException ex) when (ex.SqlState == "P0404")
        {
            await ProblemDetailsFactory.WriteAsync(
                context,
                BoErrors.NotFoundError.Error,
                BoErrors.NotFoundError.Reason,
                (int)HttpStatusCode.NotFound);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Обработана ошибка на мидлваре");

            await ProblemDetailsFactory.WriteAsync(
                context,
                BoErrors.InternalServerError.Error,
                BoErrors.InternalServerError.Reason,
                (int)HttpStatusCode.InternalServerError);
        }
    }
}
