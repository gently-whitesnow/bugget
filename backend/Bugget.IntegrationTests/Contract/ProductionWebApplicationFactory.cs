using System.Net;
using Bugget.Application.Ports;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Тот же хост, что у <see cref="AppContractFixture"/>, но в окружении Production.
/// Не переиспользует общий хост намеренно: состав сервисов зависит от окружения,
/// а общий экземпляр поднимается один раз в development.
/// </summary>
internal sealed class ProductionWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        var fileStorageDirectory = Path.Combine(Path.GetTempPath(), "bugget-prod-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fileStorageDirectory);
        builder.UseSetting("FileStorageOptions:BaseDirectory", fileStorageDirectory);
        builder.UseSetting("DomainOptions:BaseUrl", "http://localhost");

        builder.UseSetting("ExternalSettings:Authentication:UserIdHeaderName", ContractHeaders.UserId);
        builder.UseSetting("ExternalSettings:Authentication:TeamIdHeaderName", ContractHeaders.TeamId);
        builder.UseSetting("ExternalSettings:Authentication:OrganizationIdHeaderName", ContractHeaders.WorkspaceId);
        builder.UseSetting("ExternalSettings:Authentication:WorkspaceIdHeaderName", ContractHeaders.WorkspaceId);
        builder.UseSetting("ExternalSettings:Authentication:WorkspaceRoleHeaderName", ContractHeaders.WorkspaceRole);

        var keysDirectory = Path.Combine(Path.GetTempPath(), "bugget-prod-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keysDirectory);
        builder.UseSetting("KeyStoreOptions:PemFilePath", Path.Combine(keysDirectory, "rsa_pairs.json"));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IReportPageHubClient>();
            services.AddSingleton<FakeReportPageHubClient>();
            services.AddSingleton<IReportPageHubClient>(sp => sp.GetRequiredService<FakeReportPageHubClient>());
            services.RemoveAll<ITaskQueue>();
            services.AddSingleton<ITaskQueue>(sp => new SyncTaskQueue(sp));
        });
    }
}
