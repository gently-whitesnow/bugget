using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
/// архитектурные тесты не ссылаются, и новый проект, добавленный завтра. Исходник
/// разбирается синтаксическим деревом Roslyn, а не регулярками по тексту файла: единица
/// проверки — объявленный тип со своими членами, поэтому два независимых типа в одном
/// файле не склеиваются, а обёртка, собранная наследованием (<c>Outcome&lt;T&gt; :
/// Choice&lt;T, Error&gt;</c>), видна так же, как обёртка с полями.
/// </summary>
public class ResultAbstractionRulesTests
{
    private static readonly string[] ForbiddenProjectNames = ["Monade", "Flow"];

    private static readonly string[] SuccessFlagNames = ["IsSuccess", "IsFailure", "IsError", "HasError"];

    [Fact(DisplayName = "Новых Result-подобных обёрток нет: успех и ошибка не заворачиваются в тип")]
    public void No_result_like_wrapper_types()
    {
        var violations = new List<string>();

        foreach (var (project, file, text) in ProductSources())
        {
            violations.AddRange(ResultLikeDeclarations(text)
                .Select(type => $"{file}: {type} (проект {project})"));
        }

        violations.Should().BeEmpty(
            "payload/value или признак успеха рядом с ошибкой — это Result-монада, " +
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
    [InlineData("public sealed class Outcome<T> { public T? Payload; public Error? Failure; }")]
    public void Result_like_fixture_is_rejected(string source)
    {
        ResultLikeDeclarations(source).Should().NotBeEmpty();
    }

    [Fact(DisplayName = "Гейт краснеет на generic Result-обёртке, собранной через наследование")]
    public void Inherited_generic_result_like_fixture_is_rejected()
    {
        const string source = "public sealed class Outcome<T> : Choice<T, Error> { }";

        ResultLikeDeclarations(source).Should().Equal("Outcome");
    }

    [Fact(DisplayName = "Гейт краснеет на Result-обёртке с qualified generic-базой")]
    public void Qualified_inherited_generic_result_like_fixture_is_rejected()
    {
        const string source = "public sealed class Outcome<T> : Contracts.Choice<T, Error> { }";

        ResultLikeDeclarations(source).Should().Equal("Outcome");
    }

    [Fact(DisplayName = "Гейт краснеет на обёртке, у которой payload свой, а ошибка из базы")]
    public void Inherited_error_with_own_payload_is_rejected()
    {
        const string source = """
            public sealed class Outcome<T> : ErrorCarrier<Error>
            {
                public T? Value { get; init; }
            }
            """;

        ResultLikeDeclarations(source).Should().Equal("Outcome");
    }

    [Fact(DisplayName = "Гейт оставляет нативный tuple допустимым")]
    public void Native_tuple_fixture_is_allowed()
    {
        const string source = "public sealed class Service { public (T? Value, Error? Error) Execute<T>() => default; }";

        ResultLikeDeclarations(source).Should().BeEmpty();
    }

    [Fact(DisplayName = "Гейт не склеивает Error одного типа с payload другого типа")]
    public void Independent_types_in_one_file_are_allowed()
    {
        const string source = """
            public sealed class FailureState
            {
                public Error? Error { get; init; }
            }

            public sealed class PageState
            {
                public string Content { get; init; } = string.Empty;
            }
            """;

        ResultLikeDeclarations(source).Should().BeEmpty();
    }

    [Fact(DisplayName = "Гейт не склеивает вложенный тип с payload внешнего")]
    public void Nested_type_members_do_not_leak_to_the_outer_type()
    {
        const string source = """
            public sealed class Page
            {
                public string Content { get; init; } = string.Empty;

                public sealed class Failure
                {
                    public Error? Error { get; init; }
                }
            }
            """;

        ResultLikeDeclarations(source).Should().BeEmpty();
    }

    [Fact(DisplayName = "Гейт оставляет иерархию самих ошибок и справочники ошибок допустимыми")]
    public void Error_hierarchy_and_error_catalogs_are_allowed()
    {
        const string source = """
            public abstract record Error(string Code, string Title);

            public sealed record NotFoundError(string Code, string Title) : Error(Code, Title);

            public static class BoErrors
            {
                public static readonly NotFoundError NotFound = new NotFoundError("not_found", "Нет объекта");
                public static BadRequestError Invalid(string reason) => new BadRequestError("invalid", reason);
            }
            """;

        ResultLikeDeclarations(source).Should().BeEmpty();
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

    /// <summary>
    /// Имена объявленных в исходнике типов, которые совмещают ошибку с payload или с
    /// признаком успеха. Проверяется каждый тип отдельно: члены вложенного типа
    /// принадлежат вложенному типу, а не внешнему.
    /// </summary>
    private static IReadOnlyList<string> ResultLikeDeclarations(string source) =>
        [.. CSharpSyntaxTree
            .ParseText(source)
            .GetRoot()
            .DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Where(IsResultLike)
            .Select(type => type.Identifier.ValueText)];

    private static bool IsResultLike(TypeDeclarationSyntax type)
    {
        // Ошибка бывает объявлена полем, свойством или позиционным параметром, а бывает
        // унаследована: `Outcome<T> : Choice<T, Error>` — та же обёртка, просто её половина
        // живёт в базовом типе.
        var baseTypeArguments = BaseTypeArguments(type);
        var carriesInheritedError = baseTypeArguments.Any(IsErrorType);

        var dataMembers = DataMembers(type).ToArray();
        var errorMembers = dataMembers.Where(member => IsErrorType(member.Type)).ToArray();

        if (errorMembers.Length == 0 && !carriesInheritedError)
        {
            return false;
        }

        // Тип, который несёт только ошибку, — это ещё не Result: Result появляется, когда
        // рядом с ошибкой лежит значение или признак успеха.
        var carriesPayload =
            dataMembers.Length > errorMembers.Length ||
            baseTypeArguments.Any(argument => !IsErrorType(argument));

        return carriesPayload || dataMembers.Any(IsSuccessFlag);
    }

    /// <summary>
    /// Данные, которые несёт сам тип: позиционные параметры записи и нестатические поля и
    /// свойства. Статика исключена намеренно — справочник ошибок
    /// (<c>public static readonly NotFoundError …</c>) хранит ошибки, но ничего не заворачивает.
    /// </summary>
    private static IEnumerable<(string Name, TypeSyntax Type)> DataMembers(TypeDeclarationSyntax type)
    {
        foreach (var parameter in type.ParameterList?.Parameters ?? default)
        {
            if (parameter.Type is not null)
            {
                yield return (parameter.Identifier.ValueText, parameter.Type);
            }
        }

        foreach (var member in type.Members)
        {
            if (member.Modifiers.Any(SyntaxKind.StaticKeyword) || member.Modifiers.Any(SyntaxKind.ConstKeyword))
            {
                continue;
            }

            switch (member)
            {
                case PropertyDeclarationSyntax property:
                    yield return (property.Identifier.ValueText, property.Type);
                    break;

                case FieldDeclarationSyntax field:
                    foreach (var variable in field.Declaration.Variables)
                    {
                        yield return (variable.Identifier.ValueText, field.Declaration.Type);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Аргументы generic-баз: у <c>Outcome&lt;T&gt; : Choice&lt;T, Error&gt;</c> это T и Error.
    /// Само имя базового типа не смотрим — иначе под правило попала бы иерархия самих ошибок
    /// (<c>BadRequestError : Error</c>).
    /// </summary>
    private static IReadOnlyList<TypeSyntax> BaseTypeArguments(TypeDeclarationSyntax type) =>
        [.. (type.BaseList?.Types ?? default)
            .Select(baseType => baseType.Type)
            .OfType<GenericNameSyntax>()
            .SelectMany(generic => generic.TypeArgumentList.Arguments)];

    private static bool IsErrorType(TypeSyntax type)
    {
        var bare = type is NullableTypeSyntax nullable ? nullable.ElementType : type;
        var name = bare switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => null
        };

        return name == "Error";
    }

    private static bool IsSuccessFlag((string Name, TypeSyntax Type) member) =>
        SuccessFlagNames.Contains(member.Name, StringComparer.Ordinal) &&
        member.Type is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.BoolKeyword };
}
