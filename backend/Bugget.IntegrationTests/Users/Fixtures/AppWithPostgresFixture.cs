using System;
using System.IO;
using Bugget.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;


namespace Bugget.IntegrationTests.Users.Fixtures;

/// <summary>
/// Поднимает хост объединённого bugget-api для тестов модуля users. Контейнер — общий
/// с остальными интеграционными тестами: обе схемы (reports и users) накатываются в него
/// один раз в <see cref="PostgresContainerFixture"/>, поэтому второй Postgres в прогоне
/// не нужен, а процессные переменные окружения не перетирают друг друга.
/// </summary>
public class AppWithPostgresFixture(PostgresContainerFixture fixture)
        : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _db = fixture.Container;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("USERS_POSTGRES_CONNECTION_STRING", _db.GetConnectionString());

        // Модуль reports в этих тестах не используется, но его DataSource создаётся при
        // первом резолве клиента — строка нужна, чтобы регистрация не падала.
        Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", _db.GetConnectionString());

        // Ключи подписи JWT генерируются на старте во временный каталог: секретов в тестах нет.
        var keysDir = Path.Combine(Path.GetTempPath(), "bugget-users-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keysDir);
        builder.UseSetting("KeyStoreOptions:PemFilePath", Path.Combine(keysDir, "rsa_pairs.json"));
        builder.UseSetting("ExternalSettings:Authentication:TeamIdHeaderName", "X-Test-Team-Id");
        builder.UseSetting("ExternalSettings:Authentication:OrganizationIdHeaderName", "X-Test-Workspace-Id");

        builder.ConfigureTestServices(services =>
        {
            // убираем все хостед сервисы в том числе и DbUp
            services.RemoveAll<IHostedService>();
        });
    }
}
