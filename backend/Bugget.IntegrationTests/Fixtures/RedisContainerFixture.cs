using System;
using System.Threading.Tasks;
using Testcontainers.Redis;
using Xunit;

namespace Bugget.IntegrationTests.Fixtures;

/// <summary>
/// Redis для модуля authorization: ревокация refresh-токенов, кэш ротации и кэш
/// пользователя. В объединённом хосте его клиенты резолвятся при обработке обычных
/// запросов фронта, поэтому без Redis такой запрос падает 500-й ещё на конструкторе
/// контроллера.
/// </summary>
public class RedisContainerFixture : IAsyncLifetime
{
    public readonly RedisContainer Container =
        // Та же мажорная версия, что в deploy/docker-compose.yml.
        new RedisBuilder().WithImage("redis:8").Build();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();

        // Строка подключения читается из переменной окружения — так же, как в боевом
        // контуре (Bugget.Api.Authorization.Extensions.ServiceCollectionExtensions.AddDataAccess).
        Environment.SetEnvironmentVariable("REDIS_CONNECTION_STRING", Container.GetConnectionString());
    }

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}
