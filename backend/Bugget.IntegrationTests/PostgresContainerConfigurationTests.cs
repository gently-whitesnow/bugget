using System.Threading.Tasks;
using Bugget.IntegrationTests.Fixtures;
using Npgsql;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public sealed class PostgresContainerConfigurationTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task SessionTimeouts_MatchProductionConfiguration()
    {
        await using var connection = new NpgsqlConnection(postgres.Container.GetConnectionString());
        await connection.OpenAsync();

        Assert.Equal("1min", await ShowAsync(connection, "idle_in_transaction_session_timeout"));
        Assert.Equal("15min", await ShowAsync(connection, "idle_session_timeout"));
    }

    private static async Task<string> ShowAsync(NpgsqlConnection connection, string setting)
    {
        // PostgreSQL 17 renders the configured 60s in its canonical equivalent form, 1min.
        await using var command = new NpgsqlCommand($"SHOW {setting}", connection);
        return Assert.IsType<string>(await command.ExecuteScalarAsync());
    }
}
