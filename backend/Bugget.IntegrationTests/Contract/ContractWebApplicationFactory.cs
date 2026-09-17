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

/// <summary>Хост берёт строку подключения из переменной окружения от <see cref="PostgresContainerFixture"/>, как в бою.</summary>
internal sealed class ContractWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("development");

        var fileStorageDirectory = Path.Combine(Path.GetTempPath(), "bugget-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fileStorageDirectory);
        builder.UseSetting("FileStorageOptions:BaseDirectory", fileStorageDirectory);

        // Из базового адреса собираются ссылки-приглашения; значения по умолчанию у него нет.
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

            // Кроме одного: в self-hosted workspace и команда по умолчанию создаются на старте, и bootstrap фронта
            // на них рассчитывает — без него контракт users проверялся бы в небоевом состоянии.
            services.AddHostedService<WorkspaceInitializationService>();

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
