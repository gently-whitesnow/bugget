using Bugget.Application.Ports;
using Bugget.Application.Users.Workspaces;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Хост contract-тестов: имена auth-заголовков — из боевого <c>deploy/external-settings/bugget-api/external_settings.json</c>,
/// как у фронта за nginx. Хост один на прогон: у каждой фабрики свой пул соединений, и десяток упирается в лимит Postgres.
/// </summary>
public sealed class AppContractFixture
{
    private static readonly Lazy<ContractWebApplicationFactory> SharedApp =
        new(() => new ContractWebApplicationFactory());

    private readonly Lazy<ContractWebApplicationFactory> _app = SharedApp;

    /// <summary>Окружение выставляется до создания хоста: Program читает его раньше настройки хоста.</summary>
    static AppContractFixture()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "development");
        Environment.SetEnvironmentVariable("APP_DOMAIN", "http://localhost");
    }

    public IServiceProvider Services => _app.Value.Services;

    /// <summary>Клиент с identity-заголовками, как после успешного <c>auth_request</c> в nginx.</summary>
    public HttpClient CreateAuthorizedClient(string workspaceId, string teamId, string userId, string role = "owner")
    {
        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Add(ContractHeaders.UserId, userId);
        client.DefaultRequestHeaders.Add(ContractHeaders.TeamId, teamId);
        client.DefaultRequestHeaders.Add(ContractHeaders.WorkspaceId, workspaceId);
        client.DefaultRequestHeaders.Add(ContractHeaders.WorkspaceRole, role);
        return client;
    }

    public HttpClient CreateAnonymousClient() => _app.Value.CreateClient();

    /// <summary>Для проверки провайдеров входа: redirect и auth-cookie — часть ответа до перехода.</summary>
    public HttpClient CreateAnonymousClientWithoutRedirects() => _app.Value.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public Uri BaseAddress => _app.Value.Server.BaseAddress;

    /// <summary>Транспорт для SignalR-клиента: сокета у <c>TestServer</c> нет, клиент ходит long polling'ом.</summary>
    public HttpMessageHandler CreateHandler() => _app.Value.Server.CreateHandler();
}
