using System.Text.RegularExpressions;
using FluentAssertions;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Способ вернуть ошибку в решении ровно один — нативный кортеж
/// <c>(T? Value, Error? Error)</c>, для операции без значения — <c>Error?</c> (ADR-0004).
///
/// До этого их было два: <c>Monade</c> и <c>Flow</c>, две одинаковые Result-монады, и выбор
/// между ними определялся тем, из какого модуля вырос код. Правило не даёт завести третью:
/// краснеет на объявленном типе, который совмещает ошибку с payload/value или признаком
/// успеха. Кортеж под правило не попадает: это не объявленный тип.
///
/// Правило читает исходники, а не сборки: так оно видит и те проекты, на которые
/// архитектурные тесты не ссылаются, и новый проект, добавленный завтра.
/// </summary>
public partial class ResultAbstractionRulesTests
{
    private static readonly string[] ForbiddenProjectNames = ["Monade", "Flow"];

    [Fact(DisplayName = "Новых Result-подобных обёрток нет: успех и ошибка не заворачиваются в тип")]
    public void No_result_like_wrapper_types()
    {
        var violations = new List<string>();

        foreach (var (project, file, text) in ProductSources())
        {
            if (!ContainsResultLikeDeclaration(text))
            {
                continue;
            }

            violations.Add($"{file} (проект {project})");
        }

        violations.Should().BeEmpty(
            "payload/value или признак успеха рядом с полем Error — это Result-монада, " +
            "а способ вернуть ошибку " +
            "в решении один: кортеж (T? Value, Error? Error), для операции без значения — Error? " +
            "(ADR-0004). Так уже было дважды: Monade для Bugget.* и Flow для Users.*, и выбор " +
            "между ними определялся историей кода, а не смыслом. Нарушения: {0}. " +
            "Если нужен не результат операции, а доменное состояние — назови его по домену и " +
            "не давай ему поля Error.",
            string.Join("; ", violations));
    }

    [Theory(DisplayName = "Гейт краснеет на прежних монадах и типах payload-or-error при любом имени payload")]
    [InlineData("public record struct MonadeStruct<T> { public T? Value { get; init; } public Error? Error { get; init; } }")]
    [InlineData("public record struct ResultStruct { public Error? Error { get; init; } public bool IsSuccess => Error is null; }")]
    [InlineData("public sealed record Result<T>(T? Value, Error? Error);")]
    [InlineData("public sealed record Outcome<T>(T? Data, Error? Error);")]
    [InlineData("public sealed record Outcome<T> { public T? Data { get; init; } public Error? Error { get; init; } }")]
    public void Result_like_fixture_is_rejected(string source)
    {
        ContainsResultLikeDeclaration(source).Should().BeTrue();
    }

    [Fact(DisplayName = "Гейт оставляет нативный tuple допустимым")]
    public void Native_tuple_fixture_is_allowed()
    {
        const string source = "public sealed class Service { public (T? Value, Error? Error) Execute<T>() => default; }";

        ContainsResultLikeDeclaration(source).Should().BeFalse();
    }

    [Fact(DisplayName = "Проектов Monade и Flow в решении нет")]
    public void Dropped_projects_do_not_come_back()
    {
        var returned = ForbiddenProjectNames
            .Where(SolutionGraph.Projects.ContainsKey)
            .ToArray();

        returned.Should().BeEmpty(
            "проекты {0} убраны из решения вместе с Result-монадами (ADR-0004) — " +
            "вернувшийся csproj с тем же именем означает, что решение отменили молча. " +
            "Вернулись: {1}.",
            string.Join(", ", ForbiddenProjectNames),
            string.Join(", ", returned));
    }

    /// <summary>Исходники продуктовых проектов: без тестов, bin/obj и сгенерированного кода.</summary>
    private static IEnumerable<(string Project, string File, string Text)> ProductSources()
    {
        var backend = SolutionGraph.BackendRoot;

        foreach (var project in SolutionGraph.Projects.Values.Where(p => !p.IsTestProject))
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
                .Where(path => !path.EndsWith(".g.cs", StringComparison.Ordinal));

            foreach (var file in files)
            {
                yield return (project.Name, Path.GetRelativePath(backend, file), File.ReadAllText(file));
            }
        }
    }

    private static bool ContainsResultLikeDeclaration(string source) =>
        PrimaryConstructorWrapperPattern().IsMatch(source) ||
        (ErrorMemberPattern().IsMatch(source) &&
         (PayloadMemberPattern().IsMatch(source) || SuccessFlagPattern().IsMatch(source)));

    [GeneratedRegex(@"\b(?:public|internal|protected|private)\s+bool\s+(IsSuccess|IsFailure|IsError|HasError)\b")]
    private static partial Regex SuccessFlagPattern();

    [GeneratedRegex(@"\b(?:public|internal|protected|private)\s+Error\??\s+Error\s*(\{|=>|;)")]
    private static partial Regex ErrorMemberPattern();

    [GeneratedRegex(@"\b(?:public|internal|protected|private)\s+[\w<>,.?\[\]]+\s+(?!Error\b)\w+\s*(\{|=>|;)")]
    private static partial Regex PayloadMemberPattern();

    [GeneratedRegex(@"\brecord(?:\s+(?:class|struct))?\s+\w+(?:\s*<[^>{};]+>)?\s*\((?=[^;{}]*,)(?=[^;{}]*\bError\??\s+Error\b)[^;{}]*\)\s*(?:;|\{)")]
    private static partial Regex PrimaryConstructorWrapperPattern();
}
