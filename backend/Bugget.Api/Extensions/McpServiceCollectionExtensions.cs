using Bugget.Api.Mcp;

namespace Bugget.Api.Extensions;

/// <summary>Регистрация MCP вынесена из <see cref="ServiceCollectionExtensions"/>: тот файл уже в бейзлайне
/// по fan-out, и каждый новый using там раздувает снимок.</summary>
internal static class McpServiceCollectionExtensions
{
    public static IServiceCollection AddMcp(this IServiceCollection services)
    {
        // Имя сервера задано явно: default берётся из entry assembly, а клиенты MCP показывают его пользователю.
        // Пустую ToolCollection не присваиваем: делегат AddMcpServer выполняется после того, как сборщик опций
        // сложил туда tools из контейнера, и пустой список затёр бы их.
        // IHttpContextAccessor нужен инструментам ради identity запроса: MCP-эндпоинт стоит за той же
        // header-trust схемой, что контроллеры.
        services.AddHttpContextAccessor();
        services.AddSingleton<McpAttachmentContent>();
        services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "bugget-api", Version = "1.0.0" })
            .WithHttpTransport()
            .WithTools<ReportsReadTools>()
            .WithTools<ReportsWriteTools>();

        return services;
    }
}
