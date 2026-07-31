using Bugget.Application.Ports;
using Bugget.Domain.Settings;

namespace Bugget.Infrastructure.ExternalClients.Kaiten;

/// <summary>
/// Настройки Kaiten для конкретного workspace
/// </summary>
public sealed record KaitenWorkspaceConfig(string Domain, string AccessToken);

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
