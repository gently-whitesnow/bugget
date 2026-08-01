using System.Net;
using Bugget.Api.Http;
using Bugget.Application.Errors;
using Bugget.Domain.Errors;
using Npgsql;

namespace Bugget.Api.Middlewares;

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
            await ProblemDetailsFactory.WriteAsync(context, new ProblemDescriptor(BoErrors.NotFoundError.Code, BoErrors.NotFoundError.Title, StatusCodes.Status404NotFound));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Обработана ошибка на мидлваре");

            await ProblemDetailsFactory.WriteAsync(context, CommonProblemDescriptors.InternalServerError);
        }
    }
}
