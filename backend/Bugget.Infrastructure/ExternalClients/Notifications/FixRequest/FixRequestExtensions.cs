using Bugget.Application.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace Bugget.Infrastructure.ExternalClients.Notifications.FixRequest;

/// <summary>
/// Регистрация сигнала раннеру агента — по образцу Mattermost-уведомлений:
/// глобальная конфигурация через env, без env — no-op. Порт при этом
/// регистрируется всегда: use-case зависит от него безусловно, а «раннера нет» —
/// штатное состояние self-hosted установки, а не ошибка.
/// </summary>
public static class FixRequestExtensions
{
    public const string WebhookUrlEnv = "FIX_REQUEST_WEBHOOK_URL";

    public static IServiceCollection AddBugFixRequestNotifications(this IServiceCollection services)
    {
        var webhookUrl = Environment.GetEnvironmentVariable(WebhookUrlEnv);
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            services.AddSingleton<IBugFixRequestedNotifier, NoOpBugFixRequestedNotifier>();
            return services;
        }

        services.AddHttpClient(BugFixRequestedWebhookNotifier.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(webhookUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        services.AddSingleton<IBugFixRequestedNotifier, BugFixRequestedWebhookNotifier>();

        return services;
    }
}
