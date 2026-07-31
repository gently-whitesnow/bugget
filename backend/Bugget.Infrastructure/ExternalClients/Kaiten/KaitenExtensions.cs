using Bugget.Application.Interfaces;
using Bugget.Application.Services.Settings;
using Bugget.Infrastructure.ExternalClients.Kaiten.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bugget.Infrastructure.ExternalClients.Kaiten;

public static class KaitenExtensions
{
    public static IServiceCollection AddKaiten(this IServiceCollection services)
    {
        // HttpClientFactory для создания клиентов без предустановленных настроек
        services.AddHttpClient();

        // Фабрика для создания клиентов с настройками из БД
        services.AddSingleton<KaitenClientFactory>();

        // Провайдер кэша досок по workspace
        services.AddSingleton<KaitenBoardsProvider>();

        // Сервис для lazy загрузки и перезагрузки досок
        services.AddSingleton<KaitenBoardsLoaderService>();

        // Сервис для автокомплита
        services.AddSingleton<KaitenBoardsService>();

        // Сервис для работы с настройками Kaiten
        services.AddSingleton<KaitenConfigService>();

        // Сервис для поиска (регистрируем и как синглтон, и как реализацию интерфейса)
        services.AddSingleton<KaitenSearchService>();
        services.AddSingleton<IExternalSearchRepository>(sp => sp.GetRequiredService<KaitenSearchService>());

        // Сервис для применения результатов поиска (прикрепление ссылки, комментарий)
        services.AddSingleton<KaitenApplyService>();
        services.AddSingleton<IExternalApplyRepository>(sp => sp.GetRequiredService<KaitenApplyService>());

        // Процессоры настроек
        services.AddSingleton<IWorkspaceSettingsProcessor, KaitenWorkspaceSettingsProcessor>();
        services.AddSingleton<ITeamSettingsProcessor, KaitenTeamSettingsProcessor>();

        return services;
    }
}
