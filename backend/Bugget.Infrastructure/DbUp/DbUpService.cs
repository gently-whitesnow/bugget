using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bugget.Domain.Constants;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Transactions;
using DbUp.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bugget.Infrastructure.DbUp;

public sealed class DbUpService(ILogger<DbUpService> logger) : IHostedService
{
    // Журнал `schemaversions` хранит имена в старом namespace `Bugget.DbUp.sql.<file>`, а имена embedded-ресурсов
    // с тех пор менялись дважды — поэтому в Pass 1 переименовываем их при записи в журнал, иначе применённые
    // миграции запустятся повторно. LegacyJournalNamespace править нельзя: оно лежит строками в боевом журнале.
    private const string MigrationsNamespace = "Bugget.Infrastructure.DbUp.sql.migrations";
    private const string FunctionsNamespace = "Bugget.Infrastructure.DbUp.sql.functions";
    private const string LegacyJournalNamespace = "Bugget.DbUp.sql";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentConstants.PostgresConnectionString)
            ?? throw new InvalidOperationException($"Не задана строка подключения к Postgres, env=[{EnvironmentConstants.PostgresConnectionString}]");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogError("No connection string provided.");
            return Task.CompletedTask;
        }

        if (!Run(BuildMigrationsRunner(connectionString), "migrations"))
        {
            return Task.CompletedTask;
        }

        Run(BuildFunctionsRunner(connectionString), "functions");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Движок первого прохода — миграции с журналом. Вынесен из <see cref="StartAsync"/>, чтобы characterization
    /// обновления собирал тот же движок, что и боевой запуск.
    /// </summary>
    internal static UpgradeEngine BuildMigrationsRunner(string connectionString) => DeployChanges.To
        .PostgresqlDatabase(connectionString)
        .WithScripts(new EmbeddedSqlScriptProvider(
            Assembly.GetExecutingAssembly(),
            MigrationsNamespace,
            resource => resource.Replace(
                $"{MigrationsNamespace}.",
                $"{LegacyJournalNamespace}.",
                StringComparison.Ordinal)))
        .WithTransaction()
        .LogToConsole()
        .Build();

    /// <summary>Движок второго прохода — функции, накатываются каждый раз заново.</summary>
    internal static UpgradeEngine BuildFunctionsRunner(string connectionString) => DeployChanges.To
        .PostgresqlDatabase(connectionString)
        .WithScripts(new EmbeddedSqlScriptProvider(
            Assembly.GetExecutingAssembly(),
            FunctionsNamespace,
            resource => resource))
        .JournalTo(new NullJournal())
        .WithTransactionPerScript()
        .LogToConsole()
        .Build();

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool Run(UpgradeEngine runner, string passName)
    {
        var result = runner.PerformUpgrade();
        if (!result.Successful)
        {
            logger.LogError(result.Error, "DbUp {Pass} pass failed", passName);
            return false;
        }

        logger.LogInformation("DbUp {Pass} pass succeeded", passName);
        return true;
    }
}
