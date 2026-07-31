using FluentAssertions;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Правила уровня графа проектов: что кому разрешено объявлять в .csproj.
///
/// Все правила написаны белым списком: перечислено разрешённое, всё остальное — нарушение.
/// Чёрный список здесь не работает: он ловит только те пакеты, о которых кто-то заранее
/// подумал, и молча пропускает новый.
///
/// Правила фиксируют ТЕКУЩИЙ граф, а не целевой из ADR-0001. Отступления текущего графа
/// от целевого перечислены в <see cref="KnownDeviations"/> — по одной строке на отступление,
/// со ссылкой на ADR. Цель гейта здесь — «хуже не станет».
/// </summary>
public class SolutionGraphRulesTests
{
    /// <summary>Проекты бизнес-логики и то, что им разрешено объявлять.</summary>
    private static readonly Dictionary<string, (string[] Projects, string[] Packages)> BoAllowlist = new(StringComparer.Ordinal)
    {
        ["Bugget.BO"] = (
            Projects: ["Bugget.Analytics.Contracts", "Monade", "TaskQueue"],
            Packages: ["Mime", "SixLabors.ImageSharp", "Xabe.FFmpeg", "Xabe.FFmpeg.Downloader"]),

        ["Users.BO"] = (
            Projects: ["Bugget.Entities", "TaskQueue"],
            Packages: []),
    };

    /// <summary>Пакеты драйвера БД: разрешены только там, где перечислено.</summary>
    private static readonly string[] PersistencePackages = ["Npgsql", "Dapper"];

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

    [Fact(DisplayName = "*.Entities не ссылаются ни на один проект решения")]
    public void Entities_projects_are_leaves()
    {
        var violations = SolutionGraph.Projects.Values
            .Where(p => p.Name.EndsWith(".Entities", StringComparison.Ordinal))
            .SelectMany(p => p.ProjectReferences
                .Where(r => !KnownDeviations.TargetsFor(KnownDeviations.EntitiesProjectReferences, p.Name).Contains(r))
                .Select(r => $"{p.Name} → {r}"))
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "*.Entities — самый нижний слой: он не ссылается ни на один другой проект решения, " +
            "иначе нижний слой начинает знать про верхние и граф перестаёт читаться сверху вниз. " +
            "Лишние рёбра: {0}. " +
            "Чинится переносом нужного типа в *.Entities либо прямой ссылкой из того проекта, " +
            "который этим типом реально пользуется. Текущие отступления перечислены в " +
            "KnownDeviations.EntitiesProjectReferences — новое туда добавляется только с ADR.",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "*.BO объявляют только разрешённые проекты и пакеты")]
    public void Bo_projects_declare_only_allowlisted_dependencies()
    {
        var violations = new List<string>();

        foreach (var (project, allowed) in BoAllowlist)
        {
            var node = SolutionGraph.Projects[project];
            var allowedProjects = allowed.Projects
                .Concat(KnownDeviations.TargetsFor(KnownDeviations.BoProjectReferences, project))
                .ToHashSet(StringComparer.Ordinal);

            violations.AddRange(node.ProjectReferences
                .Where(r => !allowedProjects.Contains(r))
                .Select(r => $"{project} → проект {r}"));

            violations.AddRange(node.PackageReferences
                .Where(p => !allowed.Packages.Contains(p, StringComparer.Ordinal))
                .Select(p => $"{project} → пакет {p}"));

            if (node.Sdk != "Microsoft.NET.Sdk")
            {
                violations.Add($"{project} собран как {node.Sdk} вместо Microsoft.NET.Sdk");
            }
        }

        violations.Should().BeEmpty(
            "бизнес-логика (*.BO) зависит только от того, что перечислено в белом списке " +
            "SolutionGraphRulesTests.BoAllowlist: ни ASP.NET, ни HTTP-клиентов, ни драйвера БД. " +
            "Новое: {0}. " +
            "Если зависимость действительно нужна бизнес-логике — объяви порт (интерфейс) рядом " +
            "с вызывающим кодом и реализуй его в инфраструктурном проекте, а в белый список " +
            "добавляй только то, что не тянет транспорт и персистенс. " +
            "Текущие отступления — KnownDeviations.BoProjectReferences.",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "Npgsql и Dapper объявлены только в *.DA и *.IntegrationTests")]
    public void Persistence_driver_is_confined_to_data_access()
    {
        var violations = SolutionGraph.Projects.Values
            .Where(p => !p.Name.EndsWith(".DA", StringComparison.Ordinal))
            // Интеграционные тесты поднимают Postgres в контейнере и готовят данные до вызова
            // системы — им драйвер нужен по определению. Юнит-тестам он не нужен: unit не ходит в I/O.
            .Where(p => !p.Name.EndsWith(".IntegrationTests", StringComparison.Ordinal))
            .SelectMany(p => p.PackageReferences
                .Where(package => PersistencePackages.Contains(package, StringComparer.Ordinal))
                .Select(package => $"{p.Name} → {package}"))
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "драйвер Postgres ({0}) живёт только в data access (*.DA) и в интеграционных тестах. " +
            "Лишние объявления: {1}. " +
            "Вызов из другого слоя оформляется методом *DbClient в соответствующем *.DA и " +
            "интерфейсом к нему — тогда слой над ним тестируется без базы.",
            string.Join(" / ", PersistencePackages),
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "Известные отступления не протухли")]
    public void Known_deviations_are_still_real()
    {
        var stale = new List<string>();

        foreach (var deviation in KnownDeviations.BoProjectReferences.Concat(KnownDeviations.EntitiesProjectReferences))
        {
            if (!SolutionGraph.Projects[deviation.From].ProjectReferences.Contains(deviation.To, StringComparer.Ordinal))
            {
                stale.Add(deviation.ToString());
            }
        }

        stale.Should().BeEmpty(
            "отступление снято в коде, но осталось в списке KnownDeviations — вычеркни строку, " +
            "иначе список превращается в кладбище исключений и перестаёт означать долг. " +
            "Протухло: {0}",
            string.Join("; ", stale));
    }
}
