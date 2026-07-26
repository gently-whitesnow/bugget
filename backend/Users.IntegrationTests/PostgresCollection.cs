using Users.IntegrationTests.Fixtures;
using Xunit;

namespace Users.IntegrationTests;

[CollectionDefinition("PostgresCollection")]
public class PostgresCollection : ICollectionFixture<PostgresContainerFixture> { }
