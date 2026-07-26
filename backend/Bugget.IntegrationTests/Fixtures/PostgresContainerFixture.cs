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
            .WithImage("postgres:18-alpine")
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

