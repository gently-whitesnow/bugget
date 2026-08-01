using System.Text.RegularExpressions;
using FluentAssertions;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Границы проектов квартета держит компилятор, а не договорённость: единственный способ
/// дотянуться до internal соседнего проекта — атрибут InternalsVisibleTo. Правило проверяет
/// именно его и разрешает ровно один сценарий — проект открывает свои internal тестам.
/// </summary>
public partial class ModuleIsolationRulesTests
{
    [Fact(DisplayName = "InternalsVisibleTo — только на тестовые проекты решения")]
    public void InternalsVisibleTo_targets_test_projects_only()
    {
        var violations = new List<string>();

        foreach (var (declaringProject, target, source) in FindInternalsVisibleTo())
        {
            if (!SolutionGraph.Projects.TryGetValue(target, out var targetProject))
            {
                violations.Add($"{source}: {declaringProject} открывает internal неизвестному проекту {target}");
                continue;
            }

            if (!targetProject.IsTestProject)
            {
                violations.Add($"{source}: {declaringProject} открывает internal не тестовому проекту {target}");
                continue;
            }
        }

        violations.Should().BeEmpty(
            "InternalsVisibleTo разрешён только на тестовый проект решения: продуктовый проект, " +
            "которому открыли internal соседа, обходит границу слоя мимо компилятора. " +
            "Нарушения: {0}. Если доступ нужен продуктовому коду — проверяемое поведение " +
            "должно быть частью public-контракта проекта.",
            string.Join("; ", violations));
    }

    /// <summary>
    /// Собирает объявления InternalsVisibleTo из .csproj (ItemGroup) и из исходников
    /// (атрибут уровня сборки). Возвращает: проект-владелец, кому открыт доступ, где объявлено.
    /// </summary>
    private static IEnumerable<(string DeclaringProject, string Target, string Source)> FindInternalsVisibleTo()
    {
        var backend = SolutionGraph.BackendRoot;

        foreach (var project in SolutionGraph.Projects.Values)
        {
            var projectDir = Path.Combine(backend, project.Name);
            if (!Directory.Exists(projectDir))
            {
                continue;
            }

            var files = Directory
                .EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Concat(Directory.EnumerateFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly));

            foreach (var file in files)
            {
                foreach (Match match in InternalsVisibleToPattern().Matches(File.ReadAllText(file)))
                {
                    // Атрибут допускает "Assembly, PublicKey=..." — ключ подписи нам не важен.
                    var target = match.Groups["target"].Value.Split(',')[0].Trim();
                    yield return (project.Name, target, Path.GetRelativePath(backend, file));
                }
            }
        }
    }

    [GeneratedRegex("""InternalsVisibleTo(?:Attribute)?\s*(?:\(\s*"|\s+Include\s*=\s*")(?<target>[^"]+)"?""")]
    private static partial Regex InternalsVisibleToPattern();
}
