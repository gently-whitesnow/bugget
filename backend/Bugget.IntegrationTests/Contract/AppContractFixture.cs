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
/// Хост для characterization-тестов публичного контракта. Отличается от
/// <see cref="AppWithPostgresFixture"/> одним: имена auth-заголовков берутся не из
/// умолчаний, а из боевого <c>deploy/external-settings/bugget-api/external_settings.json</c>.
/// Только с ними тесты видят тот же pipeline, что и фронт за nginx: identity собирается
/// из <c>Auth-Request-*</c>, а запрос без них получает 401.
/// </summary>
/// <remarks>
/// Хост один на весь прогон: каждый экземпляр <c>WebApplicationFactory</c> держит
/// собственный пул соединений к Postgres, и десяток хостов на один контейнер
/// упирается в лимит соединений («sorry, too many clients already»). Классы тестов
/// получают его через общий экземпляр, а не поднимают свой.
/// </remarks>
public sealed class AppContractFixture
{
    private static readonly Lazy<ContractWebApplicationFactory> SharedApp =
        new(() => new ContractWebApplicationFactory());

    /// <summary>
    /// Переменные окружения, влияющие на состав сервисов, выставляются до создания
    /// хоста: <c>WebApplication.CreateBuilder</c> в Program читает окружение раньше,
    /// чем отрабатывает настройка хоста. В development-режиме
    /// регистрируется fake-провайдер входа и упрощается определение mime у вложений.
    /// </summary>
    static AppContractFixture()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "development");
        Environment.SetEnvironmentVariable("APP_DOMAIN", "http://localhost");
    }

    /// <summary>Сервисы работающего хоста.</summary>
    public IServiceProvider Services => SharedApp.Value.Services;

    /// <summary>
    /// Клиент с identity-заголовками ровно того вида, что проставляет nginx после
    /// успешного <c>auth_request</c>.
    /// </summary>
    public HttpClient CreateAuthorizedClient(string workspaceId, string teamId, string userId, string role = "owner")
    {
        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Add(ContractHeaders.UserId, userId);
        client.DefaultRequestHeaders.Add(ContractHeaders.TeamId, teamId);
        client.DefaultRequestHeaders.Add(ContractHeaders.WorkspaceId, workspaceId);
        client.DefaultRequestHeaders.Add(ContractHeaders.WorkspaceRole, role);
        return client;
    }

    /// <summary>
    /// Клиент без identity-заголовков — запрос, не прошедший auth_request.
    /// </summary>
    public HttpClient CreateAnonymousClient() => SharedApp.Value.CreateClient();

    /// <summary>
    /// Клиент без identity-заголовков и автоматического перехода по redirect.
    /// Нужен для проверки провайдеров входа: их redirect и auth-cookie являются
    /// частью ответа, который браузер получает до перехода.
    /// </summary>
    public HttpClient CreateAnonymousClientWithoutRedirects() => SharedApp.Value.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>Адрес тестового хоста — от него строится URL хаба.</summary>
    public Uri BaseAddress => SharedApp.Value.Server.BaseAddress;

    /// <summary>
    /// Транспорт in-memory хоста для SignalR-клиента: настоящего сокета у
    /// <c>TestServer</c> нет, поэтому клиент ходит long polling'ом через этот handler.
    /// </summary>
    public HttpMessageHandler CreateHandler() => SharedApp.Value.Server.CreateHandler();
}

/// <summary>
/// Собственно хост. Строку подключения берёт из переменной окружения, которую
/// выставил <see cref="PostgresContainerFixture"/> — так же, как это делает боевой
/// контур.
/// </summary>
internal sealed class ContractWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("development");

        var fileStorageDirectory = Path.Combine(Path.GetTempPath(), "bugget-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fileStorageDirectory);
        builder.UseSetting("FileStorageOptions:BaseDirectory", fileStorageDirectory);

        // Базовый адрес инсталляции: из него собираются ссылки-приглашения в команду.
        // В боевом контуре приходит из окружения, значения по умолчанию у него нет.
        builder.UseSetting("DomainOptions:BaseUrl", "http://localhost");

        builder.UseSetting("ExternalSettings:Authentication:UserIdHeaderName", ContractHeaders.UserId);
        builder.UseSetting("ExternalSettings:Authentication:TeamIdHeaderName", ContractHeaders.TeamId);
        builder.UseSetting("ExternalSettings:Authentication:OrganizationIdHeaderName", ContractHeaders.WorkspaceId);
        builder.UseSetting("ExternalSettings:Authentication:WorkspaceIdHeaderName", ContractHeaders.WorkspaceId);
        builder.UseSetting("ExternalSettings:Authentication:WorkspaceRoleHeaderName", ContractHeaders.WorkspaceRole);
        builder.UseSetting("ExternalSettings:Authentication:AuthMethodHeaderName", ContractHeaders.AuthMethod);

        // Ключи подписи JWT генерируются на старте во временный каталог: секретов в тестах нет.
        var keysDirectory = Path.Combine(Path.GetTempPath(), "bugget-contract-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keysDirectory);
        builder.UseSetting("KeyStoreOptions:PemFilePath", Path.Combine(keysDirectory, "rsa_pairs.json"));

        builder.ConfigureTestServices(services =>
        {
            // DbUp, поллеры и очереди не нужны: схему накатывает PostgresContainerFixture.
            services.RemoveAll<IHostedService>();

            // Кроме одного: в self-hosted режиме рабочее пространство и команда по
            // умолчанию создаются на старте, и весь bootstrap фронта (join в workspace
            // и в команду) рассчитывает, что они уже есть. Без него контракт модуля
            // users проверялся бы в состоянии, которого в бою не бывает.
            services.AddHostedService<WorkspaceInitializationService>();

            services.RemoveAll<IBugFixRequestedNotifier>();
            services.AddSingleton<RecordingBugFixRequestedNotifier>();
            services.AddSingleton<IBugFixRequestedNotifier>(sp => sp.GetRequiredService<RecordingBugFixRequestedNotifier>());

            services.RemoveAll<IReportPageHubClient>();
            services.AddSingleton<FakeReportPageHubClient>();
            services.AddSingleton<IReportPageHubClient>(sp => sp.GetRequiredService<FakeReportPageHubClient>());

            // Фоновая очередь выполняется синхронно — иначе ответ мог бы уехать
            // раньше, чем работа, которую контракт обещает сделать в рамках запроса.
            services.RemoveAll<ITaskQueue>();
            services.AddSingleton<ITaskQueue>(sp => new SyncTaskQueue(sp));
        });
    }

}
