using System;
using System.Threading;
using System.Threading.Tasks;
using Bugget.Infrastructure.DbUp;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Bugget.IntegrationTests.Fixtures;

public class PostgresContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } =
        new PostgreSqlBuilder()
            // Версия та же, что в deploy/docker-compose.yml: схема проверяется на версии поставки.
            .WithImage("postgres:17")
            // Хостов много, у каждого свой пул Npgsql, контейнер один: умолчание max_connections=100 давало
            // «sorry, too many clients already». Лимит поднят только в тестах.
            .WithCommand(
                "-c", "max_connections=500",
                "-c", "idle_in_transaction_session_timeout=60s",
                "-c", "idle_session_timeout=15min")
            .Build();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();

        Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", Container.GetConnectionString());

        // DbClient'ы users резолвятся в запросах reports (например, создание бага); без их схемы — 500.
        // Контейнер общий: имена скриптов модулей в журнале DbUp не пересекаются.
        Environment.SetEnvironmentVariable("USERS_POSTGRES_CONNECTION_STRING", Container.GetConnectionString());

        var dbUp = new DbUpService(NullLogger<DbUpService>.Instance);
        await dbUp.StartAsync(CancellationToken.None);

        var usersDbUp = new Bugget.Infrastructure.Users.DbUp.DbUpService(NullLogger<Bugget.Infrastructure.Users.DbUp.DbUpService>.Instance);
        await usersDbUp.StartAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}
