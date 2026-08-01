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
///
/// Снимок — это baseline, а не список миграций: он совпадает с началом текущего набора,
/// а всё добавленное после него обязано выбираться обновлением и в снимок не дописывается.
/// </summary>
[Collection("PostgresCollection")]
public sealed class DbUpLegacyJournalTests(PostgresContainerFixture postgres)
{
    private static readonly Assembly InfrastructureAssembly =
        typeof(Bugget.Infrastructure.AssemblyMarker).Assembly;

    private const string ReportsScriptsNamespace = "Bugget.Infrastructure.DbUp.sql.migrations";
    private const string UsersScriptsNamespace = "Bugget.Infrastructure.Users.DbUp.sql";
    private const string UsersLegacyJournalNamespace = "Users.DbUp.sql";

    [Fact(DisplayName = "Обновление reports с боевым журналом не выбирает ни одной миграции")]
    public async Task Reports_upgrade_over_legacy_journal_selects_no_scripts()
    {
        var target = await PrepareLegacyDatabaseAsync(
            databaseName: "dbup_legacy_reports",
            snapshotFile: "reports.txt",
            scriptsNamespace: ReportsScriptsNamespace);

        AssertNothingToUpgrade(
            Bugget.Infrastructure.DbUp.DbUpService.BuildMigrationsRunner(target),
            "миграции reports");

        // На валидной старой схеме второй проход обязан проходить целиком: функции
        // накатываются каждый раз заново и ссылаются на существующие таблицы.
        var functions = Bugget.Infrastructure.DbUp.DbUpService.BuildFunctionsRunner(target).PerformUpgrade();
        Assert.True(functions.Successful, Describe(functions));
    }

    /// <summary>
    /// База заказчика стоит на baseline из снимка, поэтому обновление обязано выбрать ровно
    /// миграции, добавленные после снимка, — не больше (иначе переименование в легаси-имя
    /// сломано и legacy накатится повторно) и не меньше.
    /// </summary>
    [Fact(DisplayName = "Обновление users с боевым журналом выбирает ровно миграции после baseline")]
    public async Task Users_upgrade_over_legacy_journal_selects_only_post_baseline_scripts()
    {
        var target = await PrepareLegacyDatabaseAsync(
            databaseName: "dbup_legacy_users",
            snapshotFile: "users.txt",
            scriptsNamespace: UsersScriptsNamespace);

        // Существующая база живёт с данными: TTL-инвайты удаляются вместе со строками.
        await SeedTeamInviteAsync(target);
        Assert.True(await TeamInvitesTableExistsAsync(target));
        Assert.Equal(TeamInviteFunctions.Length, await TeamInviteFunctionCountAsync(target));

        var runner = Bugget.Infrastructure.Users.DbUp.DbUpService.BuildRunner(target);

        var pending = runner.GetScriptsToExecute().Select(script => script.Name).ToArray();
        Assert.Equal(UsersPostBaselineJournalNames(), pending);

        var result = runner.PerformUpgrade();
        Assert.True(result.Successful, Describe(result));

        await AssertNoTeamInviteObjectsAsync(target);

        // Второй проход по уже обновлённой базе не выбирает ничего: миграция forward-only.
        AssertNothingToUpgrade(
            Bugget.Infrastructure.Users.DbUp.DbUpService.BuildRunner(target),
            "миграции users");
    }

    [Fact(DisplayName = "Неизвестная зависимость останавливает 022 и откатывает удаление TTL-инвайтов")]
    public async Task Users_upgrade_with_unknown_dependency_rolls_back_drop_migration()
    {
        var target = await PrepareLegacyDatabaseAsync(
            databaseName: "dbup_legacy_users_dependency",
            snapshotFile: "users.txt",
            scriptsNamespace: UsersScriptsNamespace);

        await SeedTeamInviteAsync(target);
        await ExecuteAsync(target, "CREATE VIEW unexpected_team_invites AS SELECT id FROM team_invites;");

        Assert.True(await TeamInvitesTableExistsAsync(target));
        Assert.Equal(1, await TeamInviteRowCountAsync(target));
        Assert.Equal(ExpectedTeamInviteFunctionSignatures, await TeamInviteFunctionSignaturesAsync(target));

        var runner = Bugget.Infrastructure.Users.DbUp.DbUpService.BuildRunner(target);
        Assert.Equal(UsersPostBaselineJournalNames(), runner.GetScriptsToExecute().Select(script => script.Name));

        var result = runner.PerformUpgrade();

        Assert.False(result.Successful, "022 должна fail-closed остановиться на неизвестной зависимости");
        Assert.Equal(UsersPostBaselineJournalNames().Single(), result.ErrorScript?.Name);
        var postgresError = Assert.IsType<PostgresException>(result.Error);
        Assert.Equal(PostgresErrorCodes.DependentObjectsStillExist, postgresError.SqlState);
        Assert.True(await UnexpectedTeamInvitesViewExistsAsync(target));
        Assert.True(await TeamInvitesTableExistsAsync(target));
        Assert.Equal(1, await TeamInviteRowCountAsync(target));
        Assert.Equal(ExpectedTeamInviteFunctionSignatures, await TeamInviteFunctionSignaturesAsync(target));
        Assert.False(await JournalContainsAsync(target, UsersPostBaselineJournalNames().Single()));

        var retry = Bugget.Infrastructure.Users.DbUp.DbUpService.BuildRunner(target);
        Assert.Equal(UsersPostBaselineJournalNames(), retry.GetScriptsToExecute().Select(script => script.Name));
    }

    [Fact(DisplayName = "Чистая установка users не оставляет объектов TTL-инвайтов")]
    public async Task Users_clean_install_leaves_no_team_invite_objects()
    {
        var target = await CreateEmptyDatabaseAsync("dbup_clean_users");

        var result = Bugget.Infrastructure.Users.DbUp.DbUpService.BuildRunner(target).PerformUpgrade();
        Assert.True(result.Successful, Describe(result));

        await AssertNoTeamInviteObjectsAsync(target);
    }

    /// <summary>
    /// Снимок боевого журнала — неизменяемый baseline, а не список миграций: он обязан
    /// совпадать с началом текущего набора, а всё новое живёт после него и в снимок
    /// не дописывается. Для reports снимок пока совпадает с набором целиком.
    /// </summary>
    [Fact(DisplayName = "Снимок боевого журнала совпадает с началом текущего набора миграций")]
    public void Snapshots_are_the_baseline_prefix_of_the_current_migration_set()
    {
        AssertSnapshotCoversScripts("reports.txt", ReportsScriptsNamespace);
        AssertSnapshotIsBaselinePrefix("users.txt", UsersScriptsNamespace);
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
        var target = await CreateEmptyDatabaseAsync(databaseName);

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

    private async Task<string> CreateEmptyDatabaseAsync(string databaseName)
    {
        var admin = postgres.Container.GetConnectionString();
        await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE);");
        await ExecuteAsync(admin, $"CREATE DATABASE {databaseName};");

        return new NpgsqlConnectionStringBuilder(admin) { Database = databaseName }.ConnectionString;
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
    /// Снимок обязан совпадать с началом текущего набора скриптов. Это ловит и правку самого
    /// снимка, и вставку новой миграции внутрь baseline — на базе заказчика такая миграция
    /// уже числилась бы применённой и молча не накатилась бы.
    /// </summary>
    private static void AssertSnapshotIsBaselinePrefix(string snapshotFile, string scriptsNamespace)
    {
        var snapshot = ReadSnapshot(snapshotFile).Select(FileNameOf).ToArray();
        var scripts = ScriptFileNames(scriptsNamespace);

        Assert.Equal(snapshot, scripts.Take(snapshot.Length));
    }

    /// <summary>
    /// Легаси-имена миграций, добавленных после снимка: ровно их обязано выбрать обновление
    /// базы заказчика.
    /// </summary>
    private static string[] UsersPostBaselineJournalNames() =>
    [
        .. ScriptFileNames(UsersScriptsNamespace)
            .Skip(ReadSnapshot("users.txt").Count)
            .Select(fileName => $"{UsersLegacyJournalNamespace}.{fileName}")
    ];

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

    private static readonly string[] TeamInviteFunctions =
        ["create_team_invite", "update_team_invite", "get_team_invite", "delete_team_invite", "accept_team_invite"];

    private static readonly string[] ExpectedTeamInviteFunctionSignatures =
    [
        "accept_team_invite(bytea)",
        "create_team_invite(integer, integer, bytea, timestamp with time zone)",
        "delete_team_invite(integer, integer)",
        "get_team_invite(integer)",
        "update_team_invite(integer, integer, bytea, timestamp with time zone)"
    ];

    /// <summary>Строка инвайта на валидных внешних ключах: база заказчика удаляется с данными.</summary>
    private static Task SeedTeamInviteAsync(string connectionString) => ExecuteAsync(
        connectionString,
        """
        WITH w AS (
            INSERT INTO workspaces(name) VALUES ('legacy-ws') RETURNING id
        ), t AS (
            INSERT INTO teams(workspace_id, name) SELECT id, 'legacy-team' FROM w RETURNING id, workspace_id
        )
        INSERT INTO team_invites(team_id, workspace_id, token_hash, expires_at)
        SELECT id, workspace_id, '\x00ff'::bytea, now() + interval '1 day' FROM t;
        """);

    private static async Task AssertNoTeamInviteObjectsAsync(string connectionString)
    {
        Assert.False(
            await TeamInvitesTableExistsAsync(connectionString),
            "после миграции таблица team_invites всё ещё существует");

        Assert.Equal(0, await TeamInviteFunctionCountAsync(connectionString));
    }

    private static async Task<bool> TeamInvitesTableExistsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        return await connection.ExecuteScalarAsync<bool>(
            "SELECT to_regclass('public.team_invites') IS NOT NULL;");
    }

    private static async Task<bool> UnexpectedTeamInvitesViewExistsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        return await connection.ExecuteScalarAsync<bool>(
            "SELECT to_regclass('public.unexpected_team_invites') IS NOT NULL;");
    }

    private static async Task<int> TeamInviteRowCountAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        return await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM team_invites;");
    }

    private static async Task<string[]> TeamInviteFunctionSignaturesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        var signatures = await connection.QueryAsync<string>(
            """
            SELECT p.proname || '(' || pg_catalog.oidvectortypes(p.proargtypes) || ')'
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'public' AND p.proname = ANY(@names)
            ORDER BY p.proname;
            """,
            new { names = TeamInviteFunctions });
        return signatures.ToArray();
    }

    private static async Task<bool> JournalContainsAsync(string connectionString, string scriptName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        return await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM schemaversions WHERE scriptname = @scriptName);",
            new { scriptName });
    }

    private static async Task<int> TeamInviteFunctionCountAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        return await connection.ExecuteScalarAsync<int>(
            """
            SELECT count(*)::int
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'public' AND p.proname = ANY(@names);
            """,
            new { names = TeamInviteFunctions });
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.ExecuteAsync(sql, commandTimeout: 120);
    }
}
