using System;
using Bugget.Application.Ports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Bugget.IntegrationTests.Fixtures;

public class AppWithPostgresFixture(PostgresContainerFixture fixture)
    : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _db = fixture.Container;

    /// <summary>Per-test override ReportAliasMode (default/guid/team) для SaaS-сценария.</summary>
    public string? AliasModeOverride { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", _db.GetConnectionString());

        // libmagic в test-runtime'е несовместим с file 5.46; в development контроллер
        // берёт `file.ContentType` напрямую, минуя libmagic.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "development");

        // `/file-storage` из appsettings.json не writable в CI.
        var fileStorageDir = Path.Combine(Path.GetTempPath(), "bugget-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fileStorageDir);
        builder.UseSetting("FileStorageOptions:BaseDirectory", fileStorageDir);
        builder.UseSetting("ExternalSettings:Authentication:TeamIdHeaderName", "X-Test-Team-Id");
        builder.UseSetting("ExternalSettings:Authentication:OrganizationIdHeaderName", "X-Test-Workspace-Id");

        if (!string.IsNullOrEmpty(AliasModeOverride))
        {
            builder.UseSetting("ReportAliasOptions:AliasMode", AliasModeOverride);
        }

        builder.ConfigureTestServices(services =>
        {
            // убираем все хостед сервисы в том числе и DbUp
            services.RemoveAll<IHostedService>();

            services.RemoveAll<IReportPageHubClient>();
            services.AddSingleton<FakeReportPageHubClient>();
            services.AddSingleton<IReportPageHubClient>(sp => sp.GetRequiredService<FakeReportPageHubClient>());

            // Реальный TaskQueue снят вместе с IHostedService; синхронный фейк выполняет
            // work item'ы в рамках запроса, чтобы тесты видели SignalR push.
            services.RemoveAll<ITaskQueue>();
            services.AddSingleton<ITaskQueue>(sp => new SyncTaskQueue(sp));
        });
    }
}
