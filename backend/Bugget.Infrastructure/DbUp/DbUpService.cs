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
    // Журнал `schemaversions` исторически хранил имена в namespace `Bugget.DbUp.sql.<file>`
    // (когда все скрипты лежали в одной папке отдельного проекта Bugget.DbUp). Имена
    // embedded-ресурсов с тех пор менялись дважды — при разделении на migrations/ и
    // functions/ и при слиянии проектов в Bugget.Infrastructure, — поэтому в Pass 1 мы
    // переименовываем их при записи в журнал: уже применённые миграции не должны
    // запускаться повторно. Значение LegacyJournalNamespace править нельзя: оно лежит
    // строками в боевом schemaversions.
    private const string MigrationsNamespace = "Bugget.Infrastructure.DbUp.sql.migrations";
    private const string FunctionsNamespace = "Bugget.Infrastructure.DbUp.sql.functions";
    private const string LegacyJournalNamespace = "Bugget.DbUp.sql";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentConstants.PostgresConnectionString)
            ?? throw new ApplicationException($"Не задана строка подключения к Postgres, env=[{EnvironmentConstants.PostgresConnectionString}]");

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
    /// Движок первого прохода — миграции с журналом. Вынесен из <see cref="StartAsync"/>,
    /// чтобы characterization обновления собирал ровно тот же движок, что и боевой запуск,
    /// а не свою похожую копию.
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

internal sealed class EmbeddedSqlScriptProvider(
    Assembly assembly,
    string resourceNamespace,
    Func<string, string> renameForJournal) : IScriptProvider
{
    private readonly string _prefix = resourceNamespace + ".";

    public IEnumerable<SqlScript> GetScripts(IConnectionManager connectionManager)
    {
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(_prefix, StringComparison.Ordinal)
                           && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"Embedded SQL resource {name} not found");
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return new SqlScript(renameForJournal(name), reader.ReadToEnd());
            })
            .ToList();
    }
}
