using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Authorization;

public class ProblemDetailsMiddleware(ILogger<ProblemDetailsMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
    {
        try
        { await next(ctx); }
        catch (KeyNotFoundException ex)
        {
            logger.LogError(ex, "KeyNotFoundException: {Message}", ex.Message);
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
    }
}
