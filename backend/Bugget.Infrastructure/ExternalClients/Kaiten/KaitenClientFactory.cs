using Bugget.Application.Ports;
using Bugget.Domain.Settings;

namespace Bugget.Infrastructure.ExternalClients.Kaiten;

/// <summary>
/// Фабрика для создания KaitenClient с настройками из БД для конкретного workspace.
/// </summary>
public sealed class KaitenClientFactory(
    IHttpClientFactory httpClientFactory)
{
    public KaitenClient CreateClient(KaitenWorkspaceConfig config)
    {
        return new KaitenClient(httpClientFactory, config.Domain, config.AccessToken);
    }
}
