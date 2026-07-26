using System;
using System.Threading;
using System.Threading.Tasks;
using Bugget.DbUp;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Bugget.IntegrationTests.Fixtures;

public class PostgresContainerFixture : IAsyncLifetime
{
    public readonly PostgreSqlContainer Container =
        new PostgreSqlBuilder()
            // Версия та же, что в deploy/docker-compose.yml, и одна на все интеграционные
            // проекты: тесты проверяют схему на той версии, с которой поставляется сборка,
            // а второй образ в прогоне не нужен.
            .WithImage("postgres:17")
            .Build();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();

        Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", Container.GetConnectionString());

        // Накатываем скрипты через DbUpService
        var dbUp = new DbUpService(NullLogger<DbUpService>.Instance);
        await dbUp.StartAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

