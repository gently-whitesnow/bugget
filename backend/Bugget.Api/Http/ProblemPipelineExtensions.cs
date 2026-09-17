using Microsoft.AspNetCore.Builder;

namespace Bugget.Api.Http;

public static class ProblemPipelineExtensions
{
    /// <summary>
    /// Закрывает ответы фреймворка без тела (404, 405, 401 от challenge, 403 от <c>Forbid()</c>, пустые
    /// <c>Unauthorized()</c>/<c>NotFound()</c>); уже собранный problem+json не трогает.
    /// Ставится до маршрутизации и аутентификации — иначе их ответы пройдут мимо.
    /// </summary>
    public static IApplicationBuilder UseProblemStatusCodes(this IApplicationBuilder app) =>
        app.UseStatusCodePages(context =>
            ProblemDetailsFactory.WriteAsync(
                context.HttpContext,
                CommonProblemDescriptors.ForStatus(context.HttpContext.Response.StatusCode)));
}
