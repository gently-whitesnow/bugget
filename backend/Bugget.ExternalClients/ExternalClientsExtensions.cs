using Bugget.ExternalClients.Kaiten;
using Bugget.ExternalClients.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bugget.ExternalClients;

public static class ExternalClientsExtensions
{
    public static IServiceCollection AddExternalClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNotifications();
        services.AddKaiten();
        return services;
    }
}
