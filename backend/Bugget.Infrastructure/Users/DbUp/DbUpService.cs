using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Bugget.Domain.Users;
using Bugget.Infrastructure.DbUp;
using DbUp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bugget.Infrastructure.Users.DbUp;

public sealed class DbUpService(ILogger<DbUpService> logger) : IHostedService
{
    // Скрипты модуля users лежат в той же сборке, что и скрипты reports: после слияния
    // проектов в Bugget.Infrastructure перебор всех embedded-ресурсов накатил бы чужую
    // схему в users_db. Поэтому берём ровно свой префикс.
    private const string ScriptsNamespace = "Bugget.Infrastructure.Users.DbUp.sql";

    // Журнал `schemaversions` в users_db хранит имена времён отдельного проекта Users.DbUp.
    // Значение править нельзя: иначе уже применённые миграции накатятся повторно.
    private const string LegacyJournalNamespace = "Users.DbUp.sql";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable(Constants.PostgresConnectionStringEnv)
                                                                            ?? throw new ApplicationException($"Не задана строка подключения к Postgres, env=[{Constants.PostgresConnectionStringEnv}]");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogError("No connection string provided.");
            return Task.CompletedTask;
        }

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScripts(new EmbeddedSqlScriptProvider(
                Assembly.GetExecutingAssembly(),
                ScriptsNamespace,
                resource => resource.Replace(
                    $"{ScriptsNamespace}.",
                    $"{LegacyJournalNamespace}.",
                    StringComparison.Ordinal)))
            .WithTransaction()
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            logger.LogError(result.Error, "Migration failed");
        }
        else
        {
            logger.LogInformation("Database migration successful");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
