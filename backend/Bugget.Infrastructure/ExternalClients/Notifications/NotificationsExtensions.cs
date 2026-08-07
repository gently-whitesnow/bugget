using Bugget.Infrastructure.ExternalClients.Notifications.FixRequest;
using Bugget.Infrastructure.ExternalClients.Notifications.Mattermost;
using Microsoft.Extensions.DependencyInjection;

namespace Bugget.Infrastructure.ExternalClients.Notifications;

public static class NotificationsExtensions
{
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddMattermostNotifications();
        services.AddBugFixRequestNotifications();
        // todo any messenger

        return services;
    }
}
