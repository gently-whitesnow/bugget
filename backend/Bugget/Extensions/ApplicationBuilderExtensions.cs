using Bugget.Configurations;
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
        app.UseMiddleware<ResultExceptionHandlerMiddleware>();
        // Обработчики ошибок модулей users и authorization: необработанное исключение -> 500
        // в формате Flow, KeyNotFoundException -> 404.
        app.UseMiddleware<Flow.ResultExceptionHandlerMiddleware>();
        app.UseMiddleware<Authorization.ProblemDetailsMiddleware>();
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
