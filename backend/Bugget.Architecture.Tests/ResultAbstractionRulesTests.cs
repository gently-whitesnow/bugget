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
///
/// Единица разбора — проект целиком, а не файл: <c>global using Failure =
/// Bugget.Entities.Errors.Error;</c> объявляется в одном файле, а переименовывает ошибку во
/// всех остальных. Псевдоним разворачивается до имени типа, поэтому обход через
/// переименование не работает — но разворачивается именно цель: псевдоним с тем же именем,
/// указывающий на посторонний тип, правило не трогает.
/// </summary>
public class ResultAbstractionRulesTests
{
    private static readonly string[] ForbiddenProjectNames = ["Monade", "Flow"];

    private static readonly string[] SuccessFlagNames = ["IsSuccess", "IsFailure", "IsError", "HasError"];

    /// <summary>Каноническая ошибка решения — <c>Bugget.Entities.Errors.Error</c>.</summary>
    private const string ErrorTypeName = "Error";

    /// <summary>Предохранитель от псевдонима, который ссылается сам на себя.</summary>
    private const int MaxAliasDepth = 8;

    private static readonly IReadOnlyDictionary<string, TypeSyntax> EmptyAliases =
        new Dictionary<string, TypeSyntax>(StringComparer.Ordinal);

    [Fact(DisplayName = "Новых Result-подобных обёрток нет: успех и ошибка не заворачиваются в тип")]
    public void No_result_like_wrapper_types()
    {
        var violations = new List<string>();

        foreach (var (project, files) in ProductSources())
        {
            violations.AddRange(ResultLikeDeclarations(files)
                .Select(declaration => $"{declaration.File}: {declaration.Type} (проект {project})"));
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
    [InlineData("public sealed record Outcome<T>(T? Data, Bugget.Entities.Errors.Error? Error);")]
    public void Result_like_fixture_is_rejected(string source)
    {
        ResultLikeDeclarations(source).Should().NotBeEmpty();
    }

    [Theory(DisplayName = "Гейт краснеет на generic Result-обёртке, собранной через наследование, при любой квалификации имени")]
    [InlineData("public sealed class Outcome<T> : Choice<T, Error> { }")]
    [InlineData("public sealed class Outcome<T> : Contracts.Choice<T, Error> { }")]
    [InlineData("public sealed class Outcome<T> : Bugget.Contracts.Choice<T, Error> { }")]
    [InlineData("public sealed class Outcome<T> : global::Bugget.Contracts.Choice<T, Error> { }")]
    [InlineData("public sealed class Outcome<T> : Choice<T, Bugget.Entities.Errors.Error> { }")]
    [InlineData("public sealed class Outcome<T> : Choice<T, global::Bugget.Entities.Errors.Error> { }")]
    public void Inherited_generic_result_like_fixture_is_rejected(string source)
    {
        ResultLikeDeclarations(source).Should().Equal("Outcome");
    }

    [Theory(DisplayName = "Гейт краснеет, когда каноническая ошибка переименована using-псевдонимом")]
    [InlineData("using Failure = Bugget.Entities.Errors.Error; public sealed record Outcome<T>(T? Data, Failure? Error);")]
    [InlineData("using Failure = Bugget.Entities.Errors.Error; public sealed class Outcome<T> : Choice<T, Failure> { }")]
    [InlineData("using Failure = global::Bugget.Entities.Errors.Error; public sealed record Outcome<T>(T? Data, Failure? Problem);")]
    [InlineData("namespace App { using Failure = Bugget.Entities.Errors.Error; public sealed record Outcome<T>(T? Data, Failure? Error); }")]
    [InlineData("namespace App; using Failure = Bugget.Entities.Errors.Error; public sealed class Outcome<T> : Choice<T, Failure> { }")]
    [InlineData("using Wrapper = Bugget.Contracts.Choice<string, Bugget.Entities.Errors.Error>; public sealed class Outcome : Wrapper { }")]
    public void Aliased_error_type_fixture_is_rejected(string source)
    {
        ResultLikeDeclarations(source).Should().Equal("Outcome");
    }

    [Theory(DisplayName = "Гейт видит global using-псевдоним из другого файла проекта")]
    [InlineData("public sealed record Outcome<T>(T? Data, Failure? Error);")]
    [InlineData("public sealed class Outcome<T> : Choice<T, Failure> { }")]
    public void Globally_aliased_error_type_fixture_is_rejected(string declaration)
    {
        const string usings = "global using Failure = Bugget.Entities.Errors.Error;";

        ResultLikeDeclarations(usings, declaration).Should().Equal("Outcome");
    }

    [Theory(DisplayName = "Гейт не срабатывает на псевдониме с тем же именем, но чужой целью")]
    [InlineData("using Failure = Bugget.Entities.Reports.Failure; public sealed record Outcome<T>(T? Data, Failure? Error);")]
    [InlineData("using Failure = Bugget.Entities.Reports.Failure; public sealed class Outcome<T> : Choice<T, Failure> { }")]
    [InlineData("using Failure = ThirdParty.Error; public sealed record Outcome<T>(T? Data, Failure? Error);")]
    public void Alias_to_a_foreign_type_is_allowed(string source)
    {
        ResultLikeDeclarations(source).Should().BeEmpty();
    }

    [Fact(DisplayName = "Параметр типа затеняет одноимённый using-псевдоним")]
    public void Type_parameter_shadowing_an_error_alias_is_allowed()
    {
        const string source = "using Failure = Bugget.Entities.Errors.Error; public sealed record Page<Failure>(string Data, Failure? Value);";

        ResultLikeDeclarations(source).Should().BeEmpty();
    }

    [Fact(DisplayName = "Гейт не срабатывает на global using-псевдониме с чужой целью")]
    public void Global_alias_to_a_foreign_type_is_allowed()
    {
        const string usings = "global using Failure = Bugget.Entities.Reports.Failure;";
        const string declaration = "public sealed record Outcome<T>(T? Data, Failure? Error);";

        ResultLikeDeclarations(usings, declaration).Should().BeEmpty();
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
                public global::Bugget.Entities.Errors.Error? Error { get; init; }
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
    private static IEnumerable<(string Project, IReadOnlyList<SourceFile> Files)> ProductSources()
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
                .Where(path => !path.EndsWith(".g.cs", StringComparison.Ordinal))
                .Select(path => new SourceFile(Path.GetRelativePath(backend, path), File.ReadAllText(path)))
                .ToArray();

            yield return (project.Name, files);
        }
    }

    /// <summary>Файл проекта: путь для диагностики и текст для разбора.</summary>
    private sealed record SourceFile(string Path, string Text);

    /// <summary>Объявленный тип, признанный Result-подобным: файл и имя типа.</summary>
    private sealed record ResultLikeDeclaration(string File, string Type);

    /// <summary>Fixture-обёртка: каждая строка — отдельный файл одного проекта.</summary>
    private static IReadOnlyList<string> ResultLikeDeclarations(params string[] sources) =>
        [.. ResultLikeDeclarations([.. sources.Select((text, index) => new SourceFile($"Fixture{index}.cs", text))])
            .Select(declaration => declaration.Type)];

    /// <summary>
    /// Объявленные в проекте типы, которые совмещают ошибку с payload или с признаком успеха.
    /// Проверяется каждый тип отдельно: члены вложенного типа принадлежат вложенному типу, а
    /// не внешнему. Разбирается проект целиком, потому что <c>global using</c>-псевдоним из
    /// одного файла переименовывает тип во всех остальных.
    /// </summary>
    private static IReadOnlyList<ResultLikeDeclaration> ResultLikeDeclarations(IReadOnlyList<SourceFile> files)
    {
        var roots = files
            .Select(file => (file.Path, Root: CSharpSyntaxTree.ParseText(file.Text).GetRoot()))
            .ToArray();

        var globalAliases = new Dictionary<string, TypeSyntax>(StringComparer.Ordinal);
        var globalDirectives = roots
            .Select(tree => tree.Root)
            .OfType<CompilationUnitSyntax>()
            .SelectMany(unit => unit.Usings)
            .Where(directive => directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword) && directive.Alias is not null);

        foreach (var directive in globalDirectives)
        {
            globalAliases[directive.Alias!.Name.Identifier.ValueText] = directive.NamespaceOrType;
        }

        return
        [
            .. roots.SelectMany(tree => tree.Root
                .DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Where(type => IsResultLike(type, AliasesInScope(type, globalAliases)))
                .Select(type => new ResultLikeDeclaration(tree.Path, type.Identifier.ValueText)))
        ];
    }

    /// <summary>
    /// Псевдонимы, видимые объявленному типу: <c>global using</c> всего проекта, поверх них —
    /// <c>using</c> файла и объемлющих namespace, от внешнего к внутреннему.
    /// </summary>
    private static IReadOnlyDictionary<string, TypeSyntax> AliasesInScope(
        SyntaxNode type,
        IReadOnlyDictionary<string, TypeSyntax> globalAliases)
    {
        var aliases = new Dictionary<string, TypeSyntax>(globalAliases, StringComparer.Ordinal);

        foreach (var scope in type.Ancestors().Reverse())
        {
            var usings = scope switch
            {
                CompilationUnitSyntax unit => unit.Usings,
                BaseNamespaceDeclarationSyntax ns => ns.Usings,
                _ => default
            };

            foreach (var directive in usings.Where(directive => directive.Alias is not null))
            {
                aliases[directive.Alias!.Name.Identifier.ValueText] = directive.NamespaceOrType;
            }
        }

        return aliases;
    }

    private static bool IsResultLike(TypeDeclarationSyntax type, IReadOnlyDictionary<string, TypeSyntax> aliases)
    {
        // Ошибка бывает объявлена полем, свойством или позиционным параметром, а бывает
        // унаследована: `Outcome<T> : Choice<T, Error>` — та же обёртка, просто её половина
        // живёт в базовом типе.
        var baseTypeArguments = BaseTypeArguments(type, aliases);
        var carriesInheritedError = baseTypeArguments.Any(argument => IsErrorType(argument, aliases));

        var dataMembers = DataMembers(type).ToArray();
        var errorMembers = dataMembers.Where(member => IsErrorType(member.Type, aliases)).ToArray();

        if (errorMembers.Length == 0 && !carriesInheritedError)
        {
            return false;
        }

        // Тип, который несёт только ошибку, — это ещё не Result: Result появляется, когда
        // рядом с ошибкой лежит значение или признак успеха.
        var carriesPayload =
            dataMembers.Length > errorMembers.Length ||
            baseTypeArguments.Any(argument => !IsErrorType(argument, aliases));

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
    private static IReadOnlyList<TypeSyntax> BaseTypeArguments(
        TypeDeclarationSyntax type,
        IReadOnlyDictionary<string, TypeSyntax> aliases) =>
        [.. (type.BaseList?.Types ?? default)
            .Select(baseType => Resolve(baseType.Type, aliases))
            .OfType<GenericNameSyntax>()
            .SelectMany(generic => generic.TypeArgumentList.Arguments)];

    private static bool IsErrorType(TypeSyntax type, IReadOnlyDictionary<string, TypeSyntax> aliases) =>
        Resolve(type, aliases)?.Identifier.ValueText == ErrorTypeName;

    /// <summary>
    /// Имя типа без квалификации, без <c>?</c> и с развёрнутым псевдонимом: у
    /// <c>Contracts.Choice&lt;T, Error&gt;</c>, у <c>global::Bugget.Contracts.Choice&lt;T,
    /// Error&gt;</c> и у <c>Failure</c> при <c>using Failure = …Errors.Error;</c> получается
    /// то, чем тип является на самом деле.
    ///
    /// Псевдоним разворачивается только у неквалифицированного имени: <c>Ns.Failure</c> — это
    /// настоящий тип <c>Failure</c> в <c>Ns</c>, а не переименование, и путать их нельзя.
    /// Разворачивается именно цель псевдонима, поэтому <c>using Failure = …Reports.Failure;</c>
    /// правило не трогает.
    /// </summary>
    private static SimpleNameSyntax? Resolve(TypeSyntax type, IReadOnlyDictionary<string, TypeSyntax> aliases, int depth = 0)
    {
        var bare = type is NullableTypeSyntax nullable ? nullable.ElementType : type;

        if (bare is IdentifierNameSyntax identifier &&
            depth < MaxAliasDepth &&
            aliases.TryGetValue(identifier.Identifier.ValueText, out var target))
        {
            return Resolve(target, aliases, depth + 1);
        }

        return bare switch
        {
            QualifiedNameSyntax qualified => Resolve(qualified.Right, EmptyAliases, depth),
            AliasQualifiedNameSyntax alias => Resolve(alias.Name, EmptyAliases, depth),
            SimpleNameSyntax simple => simple,
            _ => null
        };
    }

    private static bool IsSuccessFlag((string Name, TypeSyntax Type) member) =>
        SuccessFlagNames.Contains(member.Name, StringComparer.Ordinal) &&
        member.Type is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.BoolKeyword };
}
