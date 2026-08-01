using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Bugget.IntegrationTests.Fixtures;
using Dapper;
using DbUp.Engine;
using DbUp.Engine.Output;
using DbUp.Postgresql;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

/// <summary>
/// Characterization боевого обновления: журнал <c>schemaversions</c> у заказчика заполнен
/// именами времён отдельных проектов <c>Bugget.DbUp</c> и <c>Users.DbUp</c>, а после слияния
/// в <c>Bugget.Infrastructure</c> имена embedded-ресурсов стали другими. Раннеры
/// переименовывают ресурс обратно в легаси-имя при записи в журнал — если это сломать,
/// накат на существующую базу применит все миграции второй раз.
///
/// Ключевое здесь — откуда берётся «старый» журнал. Он берётся из снимка
/// <c>data/dbup-legacy-journal/*.txt</c>, который ведётся руками и от проверяемого
/// переименования не зависит. Журнал, созданный тем же переименованием, доказывал бы
/// только самосогласованность: сдвиг префикса на сегмент прошёл бы незамеченным.
///
/// Сценарий разыгрывается на отдельных базах в том же контейнере, схема накатывается
/// напрямую скриптами и после этого не портится: проверяется обновление валидной старой
/// базы, а не восстановление после удаления таблицы.
/// </summary>
[Collection("PostgresCollection")]
public sealed class DbUpLegacyJournalTests(PostgresContainerFixture postgres)
{
    private static readonly Assembly InfrastructureAssembly =
        typeof(Bugget.Infrastructure.AssemblyMarker).Assembly;

    [Fact(DisplayName = "Обновление reports с боевым журналом не выбирает ни одной миграции")]
    public async Task Reports_upgrade_over_legacy_journal_selects_no_scripts()
    {
        var target = await PrepareLegacyDatabaseAsync(
            databaseName: "dbup_legacy_reports",
            snapshotFile: "reports.txt",
            scriptsNamespace: "Bugget.Infrastructure.DbUp.sql.migrations");

        AssertNothingToUpgrade(
            Bugget.Infrastructure.DbUp.DbUpService.BuildMigrationsRunner(target),
            "миграции reports");

        // На валидной старой схеме второй проход обязан проходить целиком: функции
        // накатываются каждый раз заново и ссылаются на существующие таблицы.
        var functions = Bugget.Infrastructure.DbUp.DbUpService.BuildFunctionsRunner(target).PerformUpgrade();
        Assert.True(functions.Successful, Describe(functions));
    }

    [Fact(DisplayName = "Обновление users с боевым журналом не выбирает ни одной миграции")]
    public async Task Users_upgrade_over_legacy_journal_selects_no_scripts()
    {
        var target = await PrepareLegacyDatabaseAsync(
            databaseName: "dbup_legacy_users",
            snapshotFile: "users.txt",
            scriptsNamespace: "Bugget.Infrastructure.Users.DbUp.sql");

        AssertNothingToUpgrade(
            Bugget.Infrastructure.Users.DbUp.DbUpService.BuildRunner(target),
            "миграции users");
    }

    [Fact(DisplayName = "Снимок боевого журнала покрывает ровно текущий набор миграций")]
    public void Snapshots_cover_exactly_the_current_migration_set()
    {
        AssertSnapshotCoversScripts("reports.txt", "Bugget.Infrastructure.DbUp.sql.migrations");
        AssertSnapshotCoversScripts("users.txt", "Bugget.Infrastructure.Users.DbUp.sql");
    }

    /// <summary>
    /// Обновление существующей базы — это ноль выбранных скриптов и успешный проход.
    /// Проверяются оба: пустой выбор без успеха ничего не значит, а успех сам по себе
    /// бывает и у прохода, который заново применил всё подряд.
    /// </summary>
    private static void AssertNothingToUpgrade(UpgradeEngine runner, string what)
    {
        var pending = runner.GetScriptsToExecute().Select(script => script.Name).ToArray();

        Assert.True(
            pending.Length == 0,
            $"{what}: раннер выбрал скрипты, которых в боевом журнале уже нет под своими " +
            $"именами — значит, переименование в легаси-имя сломано и на базе заказчика " +
            $"миграции накатятся повторно. Выбрано: {string.Join(", ", pending)}");

        var result = runner.PerformUpgrade();
        Assert.True(result.Successful, Describe(result));
        Assert.Empty(result.Scripts);
    }

    private static string Describe(DatabaseUpgradeResult result) =>
        result.Successful
            ? "проход успешен"
            : $"проход DbUp завершился ошибкой на скрипте {result.ErrorScript?.Name}: {result.Error}";

    /// <summary>
    /// Готовит базу в состоянии «до обновления»: схема накатана скриптами напрямую,
    /// журнал заполнен каноническим снимком боевых имён средствами самого DbUp.
    /// </summary>
    private async Task<string> PrepareLegacyDatabaseAsync(
        string databaseName,
        string snapshotFile,
        string scriptsNamespace)
    {
        var admin = postgres.Container.GetConnectionString();
        await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE);");
        await ExecuteAsync(admin, $"CREATE DATABASE {databaseName};");

        var target = new NpgsqlConnectionStringBuilder(admin) { Database = databaseName }.ConnectionString;

        var legacyNames = ReadSnapshot(snapshotFile);

        // Схема накатывается ровно теми же скриптами, что и в бою, но напрямую: журнал
        // здесь ни при чём, поэтому проверяемое переименование в подготовку не попадает.
        foreach (var name in legacyNames)
        {
            await ExecuteAsync(target, ReadScript(scriptsNamespace, FileNameOf(name)));
        }

        SeedLegacyJournal(target, legacyNames);

        return target;
    }

    /// <summary>
    /// Записывает канонические имена в журнал тем же классом DbUp, которым его ведёт
    /// боевой запуск: форма таблицы получается настоящей, а не воспроизведённой в тесте.
    /// </summary>
    private static void SeedLegacyJournal(string connectionString, IReadOnlyList<string> legacyNames)
    {
        var log = new ConsoleUpgradeLog();
        var connectionManager = new PostgresqlConnectionManager(connectionString);
        var journal = new PostgresqlTableJournal(() => connectionManager, () => log, "public", "schemaversions");

        connectionManager.OperationStarting(log, []);
        connectionManager.ExecuteCommandsWithManagedConnection(dbCommandFactory =>
        {
            journal.EnsureTableExistsAndIsLatestVersion(dbCommandFactory);

            foreach (var name in legacyNames)
            {
                journal.StoreExecutedScript(new SqlScript(name, string.Empty), dbCommandFactory);
            }
        });
    }

    private static void AssertSnapshotCoversScripts(string snapshotFile, string scriptsNamespace)
    {
        var snapshot = ReadSnapshot(snapshotFile).Select(FileNameOf).ToArray();
        var scripts = ScriptFileNames(scriptsNamespace);

        Assert.Equal(scripts, snapshot);
    }

    /// <summary>
    /// Читает снимок боевого журнала. Сверяется только состав файлов; сами имена в снимке
    /// записаны целиком и намеренно не собираются из префикса — иначе сдвиг префикса
    /// прошёл бы через проверку.
    /// </summary>
    private static IReadOnlyList<string> ReadSnapshot(string snapshotFile) =>
    [
        .. File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "data", "dbup-legacy-journal", snapshotFile))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .OrderBy(line => line, StringComparer.Ordinal)
    ];

    private static string[] ScriptFileNames(string scriptsNamespace) =>
    [
        .. InfrastructureAssembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(scriptsNamespace + ".", StringComparison.Ordinal)
                           && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Select(name => name[(scriptsNamespace.Length + 1)..])
            .OrderBy(name => name, StringComparer.Ordinal)
    ];

    /// <summary>Имя файла скрипта — всё, что стоит после последнего номера версии.</summary>
    private static string FileNameOf(string journalName)
    {
        var tail = journalName.Split('.');
        // <namespace>.<NNN>_<name>.sql — имя файла это два последних сегмента.
        return string.Join('.', tail[^2..]);
    }

    private static string ReadScript(string scriptsNamespace, string fileName)
    {
        var resource = $"{scriptsNamespace}.{fileName}";
        using var stream = InfrastructureAssembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException(
                $"В снимке боевого журнала есть {fileName}, а скрипта {resource} в сборке нет.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.ExecuteAsync(sql, commandTimeout: 120);
    }
}
