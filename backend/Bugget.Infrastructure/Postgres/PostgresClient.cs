using System.Text.Json;
using Bugget.Domain.Constants;
using Npgsql;

namespace Bugget.Infrastructure.Postgres;

/// <summary>
/// Общая база Postgres-адаптеров. База одна на решение, а подключений два: модуль reports
/// ходит в <c>app_db</c>, модуль users — в <c>users_db</c>. Отличает их только имя
/// env-переменной, которое наследник передаёт в конструктор.
/// </summary>
public abstract class PostgresClient
{
    protected readonly NpgsqlDataSource DataSource;

    protected readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    protected PostgresClient()
        : this(EnvironmentConstants.PostgresConnectionString)
    {
    }

    protected PostgresClient(string connectionStringEnvName)
    {
        DataSource = NpgsqlDataSource.Create(
            Environment.GetEnvironmentVariable(connectionStringEnvName)
            ?? throw new ApplicationException($"Не задана строка подключения к Postgres, env=[{connectionStringEnvName}]"));
    }
}
