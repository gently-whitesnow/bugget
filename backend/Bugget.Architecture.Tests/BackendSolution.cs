using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Bugget.Architecture.Tests;

/// <summary>Файл, который правило проверяет: путь для диагностики и дерево для связывания.</summary>
internal sealed record ScannedFile(string Path, SyntaxTree Tree);

/// <summary>
/// Продуктовый проект: его фактическая компиляция и файлы, которые предъявляются правилам.
/// Компиляция шире: в неё входит и сгенерированный код, потому что без него не связываются
/// ручные файлы.
/// </summary>
internal sealed record ProductProject(string Name, Compilation Compilation, IReadOnlyList<ScannedFile> Scanned);

/// <summary>
/// Решение <c>backend/Bugget.sln</c>, открытое так же, как его открывает сборка: через
/// MSBuild. Компиляции берутся у него готовыми — со своими compile items, ссылками,
/// implicit usings и прочими опциями проекта.
///
/// Своей модели проекта здесь нет намеренно. Собрать компиляцию руками (собрать <c>*.cs</c>
/// с диска и подставить ссылки) — значит завести второе описание того, что уже описано в
/// <c>.csproj</c>: оно разойдётся на первом же <c>Compile Remove</c>, condition или
/// проекте, который забыли добавить в ссылки тестов. Разойдётся молча — несвязанный тип
/// становится error-символом, и правило перестаёт видеть нарушение, оставаясь зелёным.
///
/// Открытие решения стоит несколько секунд на весь прогон: результат кешируется и
/// переиспользуется всеми правилами.
/// </summary>
internal static class BackendSolution
{
    private static readonly Lazy<Task<IReadOnlyList<ProductProject>>> LazyProducts = new(LoadAsync);

    /// <summary>
    /// MSBuild нужно найти до того, как Roslyn попробует загрузить его сборки, — иначе
    /// решение откроется пустым. Инициализатор модуля выполняется до первого теста.
    /// </summary>
    [ModuleInitializer]
    internal static void RegisterMsBuild()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }

    /// <summary>Проекты решения, кроме тестовых: их правила не проверяют.</summary>
    internal static Task<IReadOnlyList<ProductProject>> ProductProjectsAsync() => LazyProducts.Value;

    private static async Task<IReadOnlyList<ProductProject>> LoadAsync()
    {
        using var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(Path.Combine(SolutionGraph.BackendRoot, "Bugget.sln"));

        var failures = workspace.Diagnostics
            .Where(diagnostic => diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
            .Select(diagnostic => diagnostic.Message)
            .ToArray();

        if (failures.Length > 0)
        {
            throw new InvalidOperationException(
                "решение открылось не целиком, и правила увидели бы не весь backend: " +
                string.Join("; ", failures));
        }

        var products = new List<ProductProject>();

        foreach (var project in solution.Projects.OrderBy(project => project.Name, StringComparer.Ordinal))
        {
            if (!SolutionGraph.Projects.TryGetValue(project.Name, out var node) || node.IsTestProject)
            {
                continue;
            }

            var compilation = await project.GetCompilationAsync()
                ?? throw new InvalidOperationException($"проект {project.Name} не дал компиляции");

            var scanned = new List<ScannedFile>();

            foreach (var document in project.Documents)
            {
                if (document.FilePath is not { } path || IsGenerated(path))
                {
                    continue;
                }

                if (await document.GetSyntaxTreeAsync() is { } tree)
                {
                    scanned.Add(new ScannedFile(Path.GetRelativePath(SolutionGraph.BackendRoot, path), tree));
                }
            }

            products.Add(new ProductProject(project.Name, compilation, scanned));
        }

        return products;
    }

    /// <summary>
    /// Сгенерированное: код из OpenAPI (<c>*.g.cs</c>, ADR-0005) и то, что MSBuild кладёт в
    /// <c>obj/</c>. Правилам оно не предъявляется — руками его не пишут, — но в компиляции
    /// остаётся: без него не связывается ручной код.
    /// </summary>
    private static bool IsGenerated(string path) =>
        path.EndsWith(".g.cs", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
}
