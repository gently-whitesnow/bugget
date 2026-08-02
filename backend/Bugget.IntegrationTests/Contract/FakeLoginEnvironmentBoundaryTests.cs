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
/// Граница fake-провайдера входа по окружению. Контроллер живёт в сборке хоста и
/// попадает в application parts независимо от окружения: <c>AddFakeAuth</c>
/// (backend/Bugget.Api/Modules/ModulesExtensions.cs:45) регистрирует только опции,
/// а <c>IExternalAuthService</c> регистрируется безусловно
/// (backend/Bugget.Api/Authorization/Extensions/ServiceCollectionExtensions.cs:68).
/// В боевом контуре путь <c>/api/authorization/v1/*</c> проксируется без
/// <c>auth_request</c> (deploy/nginx/snippets/locations/01-authorization-api.conf:1),
/// поэтому единственной защитой маршрута была обязательная headers-policy
/// <see cref="Bugget.Api.Authentication.ReportsModuleAuthorizationConvention"/>.
/// </summary>
[Collection("PostgresCollection")]
public sealed class FakeLoginEnvironmentBoundaryTests
{
    [Fact(DisplayName = "GET /v1/fake/login в Production: анонимный вызов не должен выдавать сессию")]
    public async Task FakeLoginIsClosedInProduction()
    {
        var previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var previousAppDomain = Environment.GetEnvironmentVariable("APP_DOMAIN");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("APP_DOMAIN", "http://localhost");

        try
        {
            using var app = new ProductionWebApplicationFactory();
            var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var response = await client.GetAsync(
                $"/v1/fake/login?externalId=attacker-{Guid.NewGuid():N}&name=Attacker");

            var cookies = response.Headers.TryGetValues("Set-Cookie", out var values)
                ? values.ToArray()
                : [];

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain(
                cookies,
                cookie => cookie.StartsWith("access_token=", StringComparison.Ordinal)
                    || cookie.StartsWith("refresh_token=", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnvironment);
            Environment.SetEnvironmentVariable("APP_DOMAIN", previousAppDomain);
        }
    }
}

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
