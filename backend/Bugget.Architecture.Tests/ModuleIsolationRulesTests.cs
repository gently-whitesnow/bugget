using System.Text.RegularExpressions;
using FluentAssertions;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Изоляция модулей: Bugget.*, Users.*, Authorization.* разговаривают друг с другом только
/// через public-типы.
///
/// Единственный способ дотянуться до internal соседнего модуля — атрибут InternalsVisibleTo.
/// Поэтому правило проверяет именно его: если атрибута нет, доступа к internal нет и на
/// уровне компилятора. Разрешён ровно один сценарий — модуль открывает свои internal
/// собственным тестам.
/// </summary>
public partial class ModuleIsolationRulesTests
{
    [Fact(DisplayName = "InternalsVisibleTo — только на тестовые проекты своего же модуля")]
    public void InternalsVisibleTo_targets_own_module_tests_only()
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

            if (SolutionGraph.ModuleOf(target) != SolutionGraph.ModuleOf(declaringProject))
            {
                violations.Add($"{source}: {declaringProject} открывает internal тестам чужого модуля {target}");
            }
        }

        violations.Should().BeEmpty(
            "InternalsVisibleTo разрешён только на тестовый проект своего модуля: тест модуля " +
            "Users не должен видеть internal модуля Bugget, иначе граница модуля держится на " +
            "договорённости, а не на компиляторе. Нарушения: {0}. " +
            "Если тесту нужен доступ — либо тест лежит не в своём модуле (перенеси его), " +
            "либо проверяемое поведение должно быть частью public-контракта модуля.",
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
