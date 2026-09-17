using Bugget.Application.Ports;
using Bugget.Domain.Settings;

namespace Bugget.Infrastructure.ExternalClients.Kaiten;

/// <summary>
/// Настройки Kaiten для конкретного workspace
/// </summary>
public sealed record KaitenWorkspaceConfig(string Domain, string AccessToken);
