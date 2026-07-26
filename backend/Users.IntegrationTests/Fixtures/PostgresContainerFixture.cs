using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Users.DbUp;
using Xunit;

namespace Users.IntegrationTests.Fixtures;

public class PostgresContainerFixture : IAsyncLifetime
{
    public readonly PostgreSqlContainer Container =
        new PostgreSqlBuilder()
            // Тот же образ, что в deploy/docker-compose.yml и в Bugget.IntegrationTests:
            // одна версия Postgres на все интеграционные тесты, один образ в прогоне.
            .WithImage("postgres:17")
            .Build();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();

        Environment.SetEnvironmentVariable("USERS_POSTGRES_CONNECTION_STRING", Container.GetConnectionString());

        // Накатываем скрипты через специальный сервис, а не берем их из папки sql
        var dbUp = new DbUpService(NullLogger<DbUpService>.Instance);
        await dbUp.StartAsync(CancellationToken.None);
    }
    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}
