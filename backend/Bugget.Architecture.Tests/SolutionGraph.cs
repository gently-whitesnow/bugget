using System.Xml.Linq;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Граф проектов backend/. Читаем .csproj, а не сборки, намеренно: объявленная зависимость видна,
/// даже когда код ей ещё не пользуется. Правила уровня типов — в LayerDependencyRulesTests.
/// </summary>
public static class SolutionGraph
{
    /// <summary>Каталог backend/ — найден подъёмом от каталога сборки теста до Bugget.slnx.</summary>
    public static string BackendRoot { get; } = FindBackendRoot();

    public static IReadOnlyDictionary<string, ProjectNode> Projects { get; } = LoadProjects(BackendRoot);

    /// <summary>
    /// Пути от проектов до пакета драйвера БД на любую глубину ProjectReference. Граф берётся из словаря,
    /// а не из <see cref="Projects"/>, чтобы правило можно было прогнать на синтетическом графе.
    /// </summary>
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

    /// <summary>Путь цикла в графе ProjectReference (a → b → … → a) или null.</summary>
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
            if (File.Exists(Path.Combine(dir.FullName, "Bugget.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Не нашёл backend/Bugget.slnx подъёмом от {AppContext.BaseDirectory}. " +
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
