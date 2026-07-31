using Bugget.Configurations;
using Bugget.Http;
using Bugget.Hubs;
using Bugget.Middlewares;
using Serilog;

namespace Bugget.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UsePipeline(this IApplicationBuilder app)
    {
        app.UseSwaggerConfiguration();
        app.UseSerilogRequestLogging();
        // Необработанное исключение -> 500 в общем формате problem+json. Один обработчик
        // на весь процесс: у модулей users и authorization был свой, дублировавший этот.
        app.UseMiddleware<ResultExceptionHandlerMiddleware>();
        // KeyNotFoundException модулей users и authorization -> 404.
        app.UseMiddleware<Authorization.NotFoundExceptionMiddleware>();
        // Пустые ответы фреймворка (404 маршрутизации, 405, 401 challenge, 403 Forbid)
        // получают тело problem+json из общего каталога. Обязан стоять до UseRouting
        // и UseAuthentication — иначе их ответы пройдут мимо.
        app.UseProblemStatusCodes();
        app.UseCors("CorsPolicy");
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHub<ReportPageHub>("/v1/report-page-hub");
            endpoints.MapHealthChecks("/_internal/ping");
            // Контракт self-hosted-контура: healthcheck контейнера ходит на /health.
            endpoints.MapHealthChecks("/health");
        });

        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        return app;
    }
}
