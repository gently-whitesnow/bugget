using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Маршруты, которые приложение реально публикует. Читаются из таблицы маршрутов
/// живого хоста, а не из списка в коде: список устаревает молча, таблица — нет.
/// </summary>
internal static class PublicSurface
{
    public static IReadOnlyList<string> Routes(IServiceProvider services)
    {
        var endpoints = services.GetRequiredService<EndpointDataSource>().Endpoints;

        return endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(Describe)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> Describe(RouteEndpoint endpoint)
    {
        var path = "/" + endpoint.RoutePattern.RawText?.TrimStart('/');
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;

        if (methods is null || methods.Count == 0)
        {
            yield return $"* {path}";
            yield break;
        }

        foreach (var method in methods.OrderBy(m => m, StringComparer.Ordinal))
        {
            yield return $"{method} {path}";
        }
    }
}
