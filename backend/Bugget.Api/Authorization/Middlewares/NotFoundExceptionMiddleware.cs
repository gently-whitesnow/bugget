using System.Collections.Generic;
using System.Threading.Tasks;
using Bugget.Api.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Bugget.Api.Authorization;

/// <summary>
/// Единственная работа этого middleware — превратить <see cref="KeyNotFoundException"/>
/// из модулей users и authorization в 404. Прежнее имя обещало общий адаптер
/// Problem Details, которым он не был: он отдавал третью форму ошибки и публиковал текст
/// исключения. Тело теперь собирает общий адаптер, текст исключения остаётся в журнале.
/// </summary>
public class NotFoundExceptionMiddleware(ILogger<NotFoundExceptionMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
    {
        try
        { await next(ctx); }
        catch (KeyNotFoundException ex)
        {
            logger.LogError(ex, "KeyNotFoundException: {Message}", ex.Message);
            await ProblemDetailsFactory.WriteAsync(ctx, CommonProblemDescriptors.NotFound);
        }
    }
}
