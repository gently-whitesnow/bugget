using Bugget.Api.Mcp;

namespace Bugget.Api.Extensions;

/// <summary>
/// Регистрация MCP вынесена из <see cref="ServiceCollectionExtensions"/>:
/// тот файл уже в бейзлайне по fan-out, и каждый новый using там раздувает
/// снимок. Инструменты и транспорт живут рядом с остальной DI Api, просто
/// в отдельном файле.
/// </summary>
internal static class McpServiceCollectionExtensions
{
    public static IServiceCollection AddMcp(this IServiceCollection services)
    {
        // MCP-сервер в том же процессе. Модели внутри нет — она у внешнего клиента,
        // который зовёт tools. Имя сервера задано явно: default берётся из entry
        // assembly, а клиенты MCP показывают это имя пользователю в списке подключений.
        //
        // Присваивания пустой ToolCollection больше нет: каркас включал ею tools
        // capability, пока инструментов не было, но делегат AddMcpServer выполняется
        // после того, как сборщик опций сложил туда tools из контейнера, и пустой
        // список затирал бы их.
        //
        // IHttpContextAccessor нужен инструментам, чтобы взять identity того же
        // запроса: MCP-эндпоинт стоит за той же header-trust схемой, что контроллеры.
        services.AddHttpContextAccessor();
        services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "bugget-api", Version = "1.0.0" })
            .WithHttpTransport()
            .WithTools<ReportsReadTools>();

        return services;
    }
}
