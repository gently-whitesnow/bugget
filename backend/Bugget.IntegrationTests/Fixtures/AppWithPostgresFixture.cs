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

    /// <summary>
    /// Override server-wide ReportAliasMode (default/guid/team) per-test.
    /// Используется для покрытия SaaS-сценария (guid) — см. InternalCommentsControllerTests.
    /// </summary>
    public string? AliasModeOverride { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", _db.GetConnectionString());

        // libmagic в test-runtime'е (HeyRed.Mime, magic.mgc v19) не совместим с file 5.46.
        // В development-режиме `InternalAttachmentsController` использует `file.ContentType`
        // напрямую, минуя libmagic, что нам и нужно для зелёных тестов.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "development");

        // Локальная директория под attachments storage на время жизни тестового хоста —
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

            // Подменяем SignalR-хаб на фейк, чтобы тесты могли утверждать факт push'a.
            services.RemoveAll<IReportPageHubClient>();
            services.AddSingleton<FakeReportPageHubClient>();
            services.AddSingleton<IReportPageHubClient>(sp => sp.GetRequiredService<FakeReportPageHubClient>());

            // Реальный TaskQueue — BackgroundService, снятый выше через RemoveAll<IHostedService>().
            // Подменяем на синхронный фейк, чтобы work item'ы (например, AttachmentEventsService.Handle*)
            // выполнялись прямо в рамках запроса и тесты могли утверждать факт SignalR push'а.
            services.RemoveAll<ITaskQueue>();
            services.AddSingleton<ITaskQueue>(sp => new SyncTaskQueue(sp));
        });
    }
}
