using Bugget.IntegrationTests.Fixtures;
using Xunit;

namespace Bugget.IntegrationTests;

/// <summary>
/// Одна коллекция на весь проект: тесты делят контейнеры и переменные окружения
/// (POSTGRES_CONNECTION_STRING и прочие — процессные), поэтому параллельно им нельзя.
/// Redis нужен модулю authorization, который живёт в том же хосте.
/// </summary>
[CollectionDefinition("PostgresCollection")]
public class PostgresCollection :
    ICollectionFixture<PostgresContainerFixture>,
    ICollectionFixture<RedisContainerFixture>
{ }

