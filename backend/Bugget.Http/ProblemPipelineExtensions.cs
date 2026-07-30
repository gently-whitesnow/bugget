using Microsoft.AspNetCore.Builder;

namespace Bugget.Http;

public static class ProblemPipelineExtensions
{
    /// <summary>
    /// Закрывает ответы, которые формирует сам фреймворк и у которых нет тела:
    /// промах маршрутизации (404), неподходящий метод (405), отказ аутентификации
    /// (401 от challenge) и авторизации (403 от <c>Forbid()</c>), а также пустые
    /// <c>Unauthorized()</c>/<c>NotFound()</c> из контроллеров. Обработчик срабатывает
    /// только на ответе без тела и Content-Type, поэтому уже собранный problem+json
    /// он не трогает.
    ///
    /// Ставится до маршрутизации и аутентификации — иначе их ответы пройдут мимо.
    /// </summary>
    public static IApplicationBuilder UseProblemStatusCodes(this IApplicationBuilder app) =>
        app.UseStatusCodePages(context =>
            ProblemDetailsFactory.WriteAsync(
                context.HttpContext,
                CommonProblemDescriptors.ForStatus(context.HttpContext.Response.StatusCode)));
}
