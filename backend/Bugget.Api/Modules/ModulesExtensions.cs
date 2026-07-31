using Bugget.Api.Authorization.Fake;
using Bugget.Api.Authorization.Oidc;
using Bugget.Api.Modules.InProcess;
using Bugget.Application.Users.Options;
using AuthorizationModule = Bugget.Api.Authorization.Extensions.ServiceCollectionExtensions;
using UsersModule = Bugget.Api.Users.Extensions.ServiceCollectionExtensions;

namespace Bugget.Api.Modules;

/// <summary>
/// Подключение модулей users и authorization к хосту объединённого bugget-api.
/// Модули остаются отдельными проектами и сохраняют свои HTTP-контракты, но живут
/// в одном процессе: межсервисные вызовы заменены на адаптеры из <see cref="InProcess"/>.
/// </summary>
public static class ModulesExtensions
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        UsersModule.AddConfiguration(services, configuration);
        UsersModule.AddDataAccess(services, configuration);

        var selfHosted = configuration.GetRequiredSection(nameof(SelfHostedOptions)).Get<SelfHostedOptions>()
            ?? throw new InvalidOperationException($"Не задана секция {nameof(SelfHostedOptions)}");
        UsersModule.AddBusinessLogic(services, configuration, selfHosted);
        UsersModule.AddWebApi(services, configuration, env);

        return services;
    }

    public static IServiceCollection AddAuthorizationModule(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        AuthorizationModule.AddConfiguration(services, configuration);
        AuthorizationModule.AddDataAccess(services, configuration);
        AuthorizationModule.AddBusinessLogic(services);
        AuthorizationModule.AddWebApi(services, configuration);
        AuthorizationModule.AddJwtAuthentication(services, configuration);

        // Провайдеры входа. В OSS-сборке их два: OIDC для боевого контура и
        // fake-логин для локальной разработки.
        if (configuration.GetSection(nameof(OidcAuthOptions)).Get<OidcAuthOptions>()?.Enabled == true)
        {
            services.AddOidcAuth(configuration);
        }

        if (env.IsDevelopment())
        {
            services.AddFakeAuth(configuration);
        }

        return services;
    }

    /// <summary>
    /// Адаптеры вместо межсервисных HTTP-вызовов. Регистрируются в хосте: только он
    /// видит сразу все модули, сами модули друг о друге по-прежнему не знают.
    /// </summary>
    public static IServiceCollection AddInProcessModuleIntegrations(this IServiceCollection services)
    {
        services.AddSingleton<Bugget.Application.Ports.IUsersClient, UsersClientAdapter>();
        services.AddSingleton<Bugget.Api.Authorization.Interfaces.IUsersClient, AuthorizationUsersClientAdapter>();
        services.AddSingleton<Bugget.Application.Users.Ports.IAuthorizationRepository, AuthorizationCacheAdapter>();

        return services;
    }
}
