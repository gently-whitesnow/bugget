using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bugget.IntegrationTests.Fixtures;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

/// <summary>
/// Characterization на боевой инвариант обновления: журнал <c>schemaversions</c> у заказчика
/// заполнен именами времён отдельных проектов <c>Bugget.DbUp</c> и <c>Users.DbUp</c>, а после
/// слияния в <c>Bugget.Infrastructure</c> имена embedded-ресурсов стали другими. Раннеры
/// переименовывают ресурс обратно в легаси-имя при записи в журнал — если это сломать,
/// накат на существующую базу применит все миграции второй раз.
///
/// Проверка идёт на отдельных базах в том же контейнере: сценарий обновления нельзя
/// разыграть на базе, которую тесты используют как рабочую.
/// </summary>
[Collection("PostgresCollection")]
public sealed class DbUpLegacyJournalTests(PostgresContainerFixture postgres)
{
    /// <summary>Имена, которые лежат в боевом журнале и меняться не должны.</summary>
    private const string ReportsLegacyPrefix = "Bugget.DbUp.sql.";
    private const string UsersLegacyPrefix = "Users.DbUp.sql.";

    /// <summary>Пространства имён сборки: они в журнал попадать не должны.</summary>
    private static readonly string[] AssemblyNamespaces = ["Bugget.Infrastructure.", "Bugget.Api."];

    [Fact(DisplayName = "Журнал миграций reports хранит легаси-имена и не накатывает их повторно")]
    public Task Reports_migrations_keep_legacy_journal_names_and_are_not_reapplied() =>
        AssertUpgradeIsNoOpAsync(
            databaseName: "dbup_upgrade_reports",
            connectionStringEnv: "POSTGRES_CONNECTION_STRING",
            legacyPrefix: ReportsLegacyPrefix,
            probeTable: "reports",
            runMigrationsAsync: () => new Bugget.Infrastructure.DbUp.DbUpService(
                    NullLogger<Bugget.Infrastructure.DbUp.DbUpService>.Instance)
                .StartAsync(CancellationToken.None));

    [Fact(DisplayName = "Журнал миграций users хранит легаси-имена и не накатывает их повторно")]
    public Task Users_migrations_keep_legacy_journal_names_and_are_not_reapplied() =>
        AssertUpgradeIsNoOpAsync(
            databaseName: "dbup_upgrade_users",
            connectionStringEnv: "USERS_POSTGRES_CONNECTION_STRING",
            legacyPrefix: UsersLegacyPrefix,
            probeTable: "users",
            runMigrationsAsync: () => new Bugget.Infrastructure.Users.DbUp.DbUpService(
                    NullLogger<Bugget.Infrastructure.Users.DbUp.DbUpService>.Instance)
                .StartAsync(CancellationToken.None));

    /// <summary>
    /// Разыгрывает обновление: чистая база → накат → снимок журнала → удаление одной
    /// таблицы схемы → повторный накат. Если раннер перестанет узнавать легаси-имена,
    /// второй накат воссоздаст удалённую таблицу и допишет журнал.
    /// </summary>
    private async Task AssertUpgradeIsNoOpAsync(
        string databaseName,
        string connectionStringEnv,
        string legacyPrefix,
        string probeTable,
        Func<Task> runMigrationsAsync)
    {
        var target = await CreateDatabaseAsync(databaseName);
        var previous = Environment.GetEnvironmentVariable(connectionStringEnv);
        Environment.SetEnvironmentVariable(connectionStringEnv, target);

        try
        {
            await runMigrationsAsync();

            var journalAfterInstall = await ReadJournalAsync(target);

            Assert.NotEmpty(journalAfterInstall);
            Assert.All(journalAfterInstall, name => Assert.StartsWith(legacyPrefix, name, StringComparison.Ordinal));
            Assert.All(journalAfterInstall, name => Assert.All(
                AssemblyNamespaces,
                ns => Assert.DoesNotContain(ns, name, StringComparison.Ordinal)));
            Assert.True(await TableExistsAsync(target, probeTable));

            // Имитируем базу заказчика: журнал остался, а схема — та, что накатили раньше.
            // Удаляем одну таблицу, чтобы повторный накат было видно.
            await ExecuteAsync(target, $"DROP TABLE public.{probeTable} CASCADE;");

            await runMigrationsAsync();

            Assert.False(
                await TableExistsAsync(target, probeTable),
                $"повторный накат воссоздал {probeTable}: раннер не узнал легаси-имена в " +
                "schemaversions и применил миграции второй раз. На боевой базе это означало бы " +
                "повторное выполнение всех скриптов.");

            var journalAfterUpgrade = await ReadJournalAsync(target);
            Assert.Equal(journalAfterInstall, journalAfterUpgrade);
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringEnv, previous);
        }
    }

    private async Task<string> CreateDatabaseAsync(string databaseName)
    {
        var admin = postgres.Container.GetConnectionString();

        await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE);");
        await ExecuteAsync(admin, $"CREATE DATABASE {databaseName};");

        return new NpgsqlConnectionStringBuilder(admin) { Database = databaseName }.ConnectionString;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.ExecuteAsync(sql);
    }

    private static async Task<IReadOnlyList<string>> ReadJournalAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        var names = await connection.QueryAsync<string>(
            "SELECT scriptname FROM public.schemaversions ORDER BY scriptname;");
        return [.. names];
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string table)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        return await connection.ExecuteScalarAsync<bool>(
            "SELECT to_regclass(@qualified) IS NOT NULL;",
            new { qualified = $"public.{table}" });
    }
}
