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
    public readonly PostgreSqlContainer Container =
        new PostgreSqlBuilder()
            // Версия та же, что в deploy/docker-compose.yml, и одна на все интеграционные
            // проекты: тесты проверяют схему на той версии, с которой поставляется сборка,
            // а второй образ в прогоне не нужен.
            .WithImage("postgres:17")
            // Хостов в прогоне много: у каждого класса тестов свой WebApplicationFactory,
            // а у каждого хоста — свой пул соединений Npgsql. После слияния тестовых
            // проектов в один они делят один контейнер, и умолчание max_connections=100
            // упиралось в «sorry, too many clients already». Лимит поднят только в тестах;
            // deploy/docker-compose.yml не менялся.
            .WithCommand(
                "-c", "max_connections=500",
                "-c", "idle_in_transaction_session_timeout=60s",
                "-c", "idle_session_timeout=15min")
            .Build();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();

        Environment.SetEnvironmentVariable("POSTGRES_CONNECTION_STRING", Container.GetConnectionString());

        // Модуль users живёт в том же процессе, и его DbClient'ы резолвятся по ходу
        // обработки запросов модуля reports (например, при создании бага). Без строки
        // подключения такой запрос падает 500-й, поэтому схема users накатывается в тот же
        // контейнер: в общем журнале DbUp имена скриптов у модулей не пересекаются
        // (Bugget.DbUp.sql.* против Users.DbUp.sql.*), а таблицы — тем более.
        Environment.SetEnvironmentVariable("USERS_POSTGRES_CONNECTION_STRING", Container.GetConnectionString());

        // Накатываем скрипты через DbUpService
        var dbUp = new DbUpService(NullLogger<DbUpService>.Instance);
        await dbUp.StartAsync(CancellationToken.None);

        var usersDbUp = new Bugget.Infrastructure.Users.DbUp.DbUpService(NullLogger<Bugget.Infrastructure.Users.DbUp.DbUpService>.Instance);
        await usersDbUp.StartAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}
