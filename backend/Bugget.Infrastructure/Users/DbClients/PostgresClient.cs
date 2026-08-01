using System;
using System.Text.Json;
using Bugget.Domain.Users;
using Npgsql;

namespace Bugget.Infrastructure.Users.DbClients;

public abstract class PostgresClient
{
    protected readonly NpgsqlDataSource DataSource = NpgsqlDataSource.Create(Environment.GetEnvironmentVariable(Constants.PostgresConnectionStringEnv)
                                                                            ?? throw new ApplicationException($"Не задана строка подключения к Postgres, env=[{Constants.PostgresConnectionStringEnv}]"));
    protected readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
