using Bugget.Api.Authentication;
using Bugget.Api.Configurations;
using Bugget.Api.Http;
using Bugget.Api.Hubs;
using Bugget.Api.Middlewares;
using Serilog;

namespace Bugget.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UsePipeline(this IApplicationBuilder app)
    {
        app.UseSwaggerConfiguration();
        app.UseSerilogRequestLogging();
        // Необработанное исключение -> 500 problem+json; один обработчик на весь процесс.
        app.UseMiddleware<ResultExceptionHandlerMiddleware>();
        // KeyNotFoundException модулей users и authorization -> 404.
        app.UseMiddleware<Bugget.Api.Authorization.NotFoundExceptionMiddleware>();
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
            // MCP-эндпоинт — не MVC-контроллер, конвенция модуля reports его не достаёт:
            // политика вешается явно. identity приходит заголовками после auth_request nginx.
            endpoints.MapMcp("/v1/mcp").RequireAuthorization(ReportsModuleAuthorizationConvention.Policy);
            endpoints.MapHealthChecks("/_internal/ping");
            // Контракт self-hosted-контура: healthcheck контейнера ходит на /health.
            endpoints.MapHealthChecks("/health");
        });

        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        return app;
    }
}
