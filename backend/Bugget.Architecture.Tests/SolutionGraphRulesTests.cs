using FluentAssertions;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Правила уровня графа проектов: что кому разрешено объявлять в .csproj.
///
/// Все правила написаны белым списком: перечислено разрешённое, всё остальное — нарушение.
/// Чёрный список здесь не работает: он ловит только те пакеты, о которых кто-то заранее
/// подумал, и молча пропускает новый.
///
/// Здесь проверяется объявленное, а не использованное: зависимость видна даже тогда, когда
/// код ей ещё не пользуется. Правила уровня сборок живут в <see cref="LayerDependencyRulesTests"/>.
/// </summary>
public class SolutionGraphRulesTests
{
    /// <summary>Целевая раскладка ADR-0001: проект → что ему разрешено объявлять в ProjectReference.</summary>
    private static readonly Dictionary<string, string[]> AllowedProjectReferences = new(StringComparer.Ordinal)
    {
        [Quartet.Domain] = [],
        [Quartet.Contracts] = [],
        [Quartet.Application] = [Quartet.Domain],
        [Quartet.Infrastructure] = [Quartet.Application, Quartet.Domain],
        [Quartet.Api] = [Quartet.Application, Quartet.Contracts, Quartet.Domain, Quartet.Infrastructure],
    };

    /// <summary>Пакеты, разрешённые нижним слоям. Пустой список — значит только BCL.</summary>
    private static readonly Dictionary<string, string[]> AllowedPackages = new(StringComparer.Ordinal)
    {
        [Quartet.Domain] = [],
        [Quartet.Contracts] = [],
        [Quartet.Application] =
        [
            "Microsoft.Extensions.Hosting.Abstractions",
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.Options.ConfigurationExtensions",
        ],
    };

    /// <summary>Пакеты драйвера БД: разрешены только там, где перечислено.</summary>
    private static readonly string[] PersistencePackages = ["Npgsql", "Dapper"];

    [Fact(DisplayName = "В решении остались только проекты квартета и тестовые")]
    public void Solution_contains_quartet_and_tests_only()
    {
        string[] expected =
        [
            Quartet.Api, Quartet.Application, Quartet.Contracts, Quartet.Domain, Quartet.Infrastructure,
            "Bugget.Architecture.Tests", "Bugget.IntegrationTests", "Bugget.UnitTests",
        ];

        SolutionGraph.Projects.Keys
            .Should().BeEquivalentTo(expected,
                "целевая раскладка — квартет Bugget.{Api,Application,Domain,Infrastructure} плюс " +
                "Bugget.Contracts и тестовые проекты (ADR-0001). Новый csproj рядом с ними — это " +
                "возврат к зоопарку проектов, ради ухода от которого затевалось слияние.");
    }

    [Fact(DisplayName = "Граф проектов — DAG, циклов нет")]
    public void Project_graph_is_acyclic()
    {
        var cycle = SolutionGraph.FindCycle();

        cycle.Should().BeNull(
            "граф проектов обязан оставаться ациклическим (ROOT.md → «Правила, которые нельзя " +
            "нарушать молча», ADR-0001). Найден цикл: {0}. " +
            "Чинится не удалением ссылки наугад: вынеси общий контракт в нижний проект " +
            "(интерфейс — в тот слой, который его вызывает) и оставь ровно одно направление ссылки.",
            cycle is null ? string.Empty : string.Join(" → ", cycle));
    }

    [Fact(DisplayName = "Проекты квартета объявляют только разрешённые ссылки")]
    public void Quartet_projects_declare_only_allowlisted_references()
    {
        var violations = new List<string>();

        foreach (var (project, allowed) in AllowedProjectReferences)
        {
            var node = SolutionGraph.Projects[project];

            var permitted = allowed
                .Concat(KnownDeviations.TargetsFor(KnownDeviations.ApplicationProjectReferences, project))
                .ToHashSet(StringComparer.Ordinal);

            violations.AddRange(node.ProjectReferences
                .Where(reference => !permitted.Contains(reference))
                .Select(reference => $"{project} → проект {reference}"));
        }

        violations.Should().BeEmpty(
            "направление ссылок в квартете задано ADR-0001: Api → Application/Infrastructure/Contracts, " +
            "Infrastructure → Application, Application → Domain, а Domain и Contracts — листья. " +
            "Лишнее: {0}. Если слою нужен контракт снизу — объяви порт рядом с вызывающим кодом " +
            "и реализуй его в инфраструктуре.",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "Нижние слои не объявляют пакетов вне белого списка")]
    public void Lower_layers_declare_only_allowlisted_packages()
    {
        var violations = new List<string>();

        foreach (var (project, allowed) in AllowedPackages)
        {
            var node = SolutionGraph.Projects[project];

            violations.AddRange(node.PackageReferences
                .Where(package => !allowed.Contains(package, StringComparer.Ordinal))
                .Select(package => $"{project} → пакет {package}"));

            if (node.Sdk != "Microsoft.NET.Sdk")
            {
                violations.Add($"{project} собран как {node.Sdk} вместо Microsoft.NET.Sdk");
            }
        }

        violations.Should().BeEmpty(
            "домен, контракты и прикладной слой зависят только от того, что перечислено в белом " +
            "списке SolutionGraphRulesTests.AllowedPackages: ни ASP.NET, ни HTTP-клиентов, ни " +
            "драйвера БД. Новое: {0}. Если зависимость действительно нужна — объяви порт рядом " +
            "с вызывающим кодом и реализуй его в Bugget.Infrastructure.",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "Npgsql и Dapper объявлены только в Bugget.Infrastructure и интеграционных тестах")]
    public void Persistence_driver_is_confined_to_infrastructure()
    {
        var violations = SolutionGraph.Projects.Values
            .Where(project => project.Name != Quartet.Infrastructure)
            // Интеграционные тесты поднимают Postgres в контейнере и готовят данные до вызова
            // системы — им драйвер нужен по определению. Юнит-тестам он не нужен: unit не ходит в I/O.
            .Where(project => !project.Name.EndsWith(".IntegrationTests", StringComparison.Ordinal))
            .SelectMany(project => project.PackageReferences
                .Where(package => PersistencePackages.Contains(package, StringComparer.Ordinal))
                .Select(package => $"{project.Name} → {package}"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "драйвер Postgres ({0}) живёт только в Bugget.Infrastructure и в интеграционных тестах. " +
            "Лишние объявления: {1}. Вызов из другого слоя оформляется методом *DbClient в " +
            "инфраструктуре и портом к нему — тогда слой над ним тестируется без базы.",
            string.Join(" / ", PersistencePackages),
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "Нижние слои не добираются до драйвера БД ни на какой глубине ссылок")]
    public void Lower_layers_do_not_reach_persistence_driver_transitively()
    {
        // Прямую ссылку ловит правило выше. Здесь — то, ради чего разворачивали зависимость
        // (ADR-0001): нижние слои не должны получать Npgsql/Dapper и через цепочку проектов.
        var leaks = SolutionGraph.FindPersistenceDriverLeaks(
            SolutionGraph.Projects,
            [Quartet.Domain, Quartet.Contracts, Quartet.Application],
            PersistencePackages);

        leaks.Should().BeEmpty(
            "нижний слой снова дотягивается до драйвера БД по цепочке ProjectReference: {0}. " +
            "Порты объявляются в Bugget.Application/**/Ports, реализации остаются в " +
            "Bugget.Infrastructure, а ссылка идёт в обратную сторону — Infrastructure → Application. " +
            "Ссылку на инфраструктуру добавляет композиционный корень (Bugget.Api), а не Application.",
            string.Join("; ", leaks));
    }

    [Fact(DisplayName = "Правило транзитивной зависимости краснеет на подсунутом ребре")]
    public void Transitive_persistence_rule_is_provably_red()
    {
        // Гейт без доказательства красноты — это гейт, про который никто не знает,
        // работает ли он (ADR-0002). Создаём настоящие SDK-style .csproj с тем же
        // синтаксисом Include, который используется в решении, затем прогоняем полный
        // production path: XML -> ProjectNode -> транзитивный обход.
        var fixtureRoot = Path.Combine(Path.GetTempPath(), $"bugget-graph-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureRoot);

        try
        {
            WriteProject(Quartet.Application, """
                <ItemGroup>
                  <ProjectReference Include="..\Bugget.Infrastructure\Bugget.Infrastructure.csproj" />
                </ItemGroup>
                """);
            WriteProject(Quartet.Infrastructure, """
                <ItemGroup>
                  <ProjectReference Include="..\Bugget.Domain\Bugget.Domain.csproj" />
                  <PackageReference Include="Dapper" />
                  <PackageReference Include="Npgsql" />
                </ItemGroup>
                """);
            WriteProject(Quartet.Domain, string.Empty);

            var parsedGraph = SolutionGraph.LoadProjects(fixtureRoot);
            var leaks = SolutionGraph.FindPersistenceDriverLeaks(
                parsedGraph,
                [Quartet.Application],
                PersistencePackages);

            leaks.Should().Equal(
                "Bugget.Application → Bugget.Infrastructure → Dapper",
                "Bugget.Application → Bugget.Infrastructure → Npgsql");
        }
        finally
        {
            Directory.Delete(fixtureRoot, recursive: true);
        }

        void WriteProject(string name, string items)
        {
            var directory = Path.Combine(fixtureRoot, name);
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, $"{name}.csproj"),
                $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>
                  {{items}}
                </Project>
                """);
        }
    }

    [Fact(DisplayName = "Известные отступления в графе проектов не протухли")]
    public void Known_deviations_are_still_real()
    {
        var stale = KnownDeviations.ApplicationProjectReferences
            .Where(deviation => !SolutionGraph.Projects[deviation.From]
                .ProjectReferences.Contains(deviation.To, StringComparer.Ordinal))
            .Select(deviation => deviation.ToString())
            .ToArray();

        stale.Should().BeEmpty(
            "отступление снято в коде, но осталось в списке KnownDeviations — вычеркни строку, " +
            "иначе список превращается в кладбище исключений и перестаёт означать долг. " +
            "Протухло: {0}",
            string.Join("; ", stale));
    }
}
