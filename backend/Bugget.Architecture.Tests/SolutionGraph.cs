using System.Xml.Linq;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Один проект решения: как он объявлен в своём .csproj.
/// </summary>
/// <param name="Name">Имя проекта без расширения, оно же имя сборки: <c>Bugget.Application</c>.</param>
/// <param name="Sdk">Значение атрибута Sdk: <c>Microsoft.NET.Sdk</c> или <c>Microsoft.NET.Sdk.Web</c>.</param>
/// <param name="ProjectReferences">Имена проектов из ProjectReference — прямые рёбра графа.</param>
/// <param name="PackageReferences">Имена пакетов из PackageReference — прямые внешние зависимости.</param>
/// <param name="IsTestProject">Проект помечен IsTestProject.</param>
public sealed record ProjectNode(
    string Name,
    string Sdk,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<string> PackageReferences,
    bool IsTestProject);

/// <summary>
/// Граф проектов backend/, прочитанный с диска.
///
/// Правила уровня графа читают .csproj, а не скомпилированные сборки, намеренно:
/// объявленная зависимость видна даже тогда, когда код ей ещё не пользуется. Это ловит
/// сценарий «зависимость притащили заранее, использовать начнут в следующем PR».
/// Правила уровня типов (кто на что ссылается в IL) живут в LayerDependencyRulesTests.
/// </summary>
public static class SolutionGraph
{
    /// <summary>Каталог backend/ — найден подъёмом от каталога сборки теста до Bugget.sln.</summary>
    public static string BackendRoot { get; } = FindBackendRoot();

    /// <summary>Все проекты backend/, ключ — имя проекта.</summary>
    public static IReadOnlyDictionary<string, ProjectNode> Projects { get; } = LoadProjects(BackendRoot);

    /// <summary>
    /// Ищет пути, по которым <paramref name="applicationProjects"/> добираются до пакета
    /// драйвера БД — на любую глубину ProjectReference, а не только прямой ссылкой.
    ///
    /// Правило намеренно читает граф из словаря, а не из <see cref="Projects"/>: так его
    /// можно прогнать на синтетическом графе и доказать, что оно действительно краснеет
    /// (см. тест «правило транзитивной зависимости краснеет на подсунутом ребре»).
    /// </summary>
    /// <returns>По строке на найденную утечку: <c>Bugget.Application → Bugget.Infrastructure → Npgsql</c>.</returns>
    public static IReadOnlyList<string> FindPersistenceDriverLeaks(
        IReadOnlyDictionary<string, ProjectNode> projects,
        IEnumerable<string> applicationProjects,
        IReadOnlyCollection<string> persistencePackages)
    {
        var leaks = new List<string>();

        foreach (var start in applicationProjects)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            Walk(start, [start]);

            void Walk(string name, List<string> path)
            {
                if (!visited.Add(name) || !projects.TryGetValue(name, out var node))
                {
                    return;
                }

                foreach (var package in node.PackageReferences.Where(persistencePackages.Contains))
                {
                    leaks.Add($"{string.Join(" → ", path)} → {package}");
                }

                foreach (var next in node.ProjectReferences)
                {
                    Walk(next, [.. path, next]);
                }
            }
        }

        return [.. leaks.OrderBy(v => v, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Ищет цикл в графе ProjectReference. Возвращает путь цикла (a → b → … → a) или null.
    /// </summary>
    public static IReadOnlyList<string>? FindCycle()
    {
        var visited = new HashSet<string>();
        var stack = new List<string>();
        var onStack = new HashSet<string>();

        foreach (var start in Projects.Keys.OrderBy(n => n, StringComparer.Ordinal))
        {
            var cycle = Walk(start);
            if (cycle is not null)
            {
                return cycle;
            }
        }

        return null;

        IReadOnlyList<string>? Walk(string name)
        {
            if (onStack.Contains(name))
            {
                var from = stack.IndexOf(name);
                return [.. stack[from..], name];
            }

            if (!visited.Add(name) || !Projects.TryGetValue(name, out var node))
            {
                return null;
            }

            stack.Add(name);
            onStack.Add(name);

            foreach (var next in node.ProjectReferences)
            {
                var cycle = Walk(next);
                if (cycle is not null)
                {
                    return cycle;
                }
            }

            stack.RemoveAt(stack.Count - 1);
            onStack.Remove(name);
            return null;
        }
    }

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Bugget.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Не нашёл backend/Bugget.sln подъёмом от {AppContext.BaseDirectory}. " +
            "Архитектурные тесты читают .csproj с диска и запускаются из дерева репозитория.");
    }

    internal static IReadOnlyDictionary<string, ProjectNode> LoadProjects(string backendRoot)
    {
        var projects = Directory
            .EnumerateFiles(backendRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Select(Parse)
            .ToDictionary(p => p.Name, StringComparer.Ordinal);

        if (projects.Count == 0)
        {
            throw new InvalidOperationException($"В {backendRoot} не нашлось ни одного .csproj.");
        }

        return projects;
    }

    private static ProjectNode Parse(string csprojPath)
    {
        var doc = XDocument.Load(csprojPath);
        var root = doc.Root ?? throw new InvalidOperationException($"Пустой .csproj: {csprojPath}");

        var references = root
            .Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include"))
            .Where(include => include is not null)
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', Path.DirectorySeparatorChar)))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        var packages = root
            .Descendants("PackageReference")
            .Select(e => (string?)e.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        var isTestProject = root
            .Descendants("IsTestProject")
            .Any(e => string.Equals(e.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase));

        return new ProjectNode(
            Path.GetFileNameWithoutExtension(csprojPath),
            (string?)root.Attribute("Sdk") ?? string.Empty,
            references,
            packages,
            isTestProject);
    }
}
