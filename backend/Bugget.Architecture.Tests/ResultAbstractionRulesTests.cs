using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Способ вернуть ошибку в решении ровно один — нативный кортеж
/// <c>(T? Value, Error? Error)</c>, для операции без значения — <c>Error?</c> (ADR-0004).
///
/// До этого их было два: <c>Monade</c> и <c>Flow</c>, две одинаковые Result-монады, и выбор
/// между ними определялся тем, из какого модуля вырос код. Правило не даёт завести третью:
/// краснеет на объявленном типе, который совмещает каноническую ошибку с payload/value или
/// признаком успеха. Кортеж под правило не попадает: это не объявленный тип.
///
/// Правило читает исходники, а не сборки: так оно видит и те проекты, на которые
/// архитектурные тесты не ссылаются, и новый проект, добавленный завтра. Что перед ним за
/// тип, оно спрашивает у компилятора — см. <see cref="ResultLikeTypes"/>.
/// </summary>
public class ResultAbstractionRulesTests
{
    private static readonly string[] ForbiddenProjectNames = ["Monade", "Flow"];

    [Fact(DisplayName = "Новых Result-подобных обёрток нет: успех и ошибка не заворачиваются в тип")]
    public void No_result_like_wrapper_types()
    {
        var violations = ProductProjects
            .SelectMany(project => ResultLikeTypes
                .Find(project.Scanned, project.Compilation)
                .Select(declaration => $"{declaration.File}: {declaration.Type} (проект {project.Name})"))
            .ToArray();

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

    [Fact(DisplayName = "Гейт видит каноническую ошибку в компиляции каждого проекта")]
    public void Canonical_error_type_is_bound_in_every_project()
    {
        var blind = ProductProjects
            .Where(project => project.Compilation.GetTypeByMetadataName(ResultLikeTypes.CanonicalErrorMetadataName)
                is not INamedTypeSymbol { TypeKind: not TypeKind.Error })
            .Select(project => project.Name)
            .ToArray();

        blind.Should().BeEmpty(
            "правило сравнивает связанный тип с каноническим {0}. Если в компиляции проекта " +
            "этого типа нет или он неоднозначен (объявлен и исходниками, и ссылкой), связывание " +
            "отдаёт error-символ, сравнение не совпадёт никогда и гейт замолчит, оставаясь " +
            "зелёным. Проекты, в которых канонический тип не связался: {1}.",
            ResultLikeTypes.CanonicalErrorMetadataName,
            string.Join(", ", blind));
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

    [Fact(DisplayName = "Гейт краснеет на обёртке, спрятанной через лишний слой наследования")]
    public void Indirectly_inherited_result_like_fixture_is_rejected()
    {
        const string source = """
            public class Middle<T> : Choice<T, Error>;

            public sealed class Outcome<T> : Middle<T>;
            """;

        ResultLikeDeclarations(source).Should().Equal("Middle", "Outcome");
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

    [Theory(DisplayName = "Гейт не путает каноническую ошибку с посторонним типом того же имени")]
    [InlineData("public sealed record Outcome<T>(T? Data, ThirdParty.Error? Error);")]
    [InlineData("using Failure = ThirdParty.Error; public sealed record Outcome<T>(T? Data, Failure? Error);")]
    [InlineData("using Failure = ThirdParty.Error; public sealed class Outcome<T> : Choice<T, Failure> { }")]
    [InlineData("public sealed class Outcome<T> : Choice<T, ThirdParty.Error> { }")]
    public void Foreign_type_named_error_is_allowed(string source)
    {
        ResultLikeDeclarations(source).Should().BeEmpty();
    }

    [Theory(DisplayName = "Гейт не срабатывает на псевдониме с тем же именем, но чужой целью")]
    [InlineData("using Failure = Bugget.Entities.Reports.Failure; public sealed record Outcome<T>(T? Data, Failure? Error);")]
    [InlineData("using Failure = Bugget.Entities.Reports.Failure; public sealed class Outcome<T> : Choice<T, Failure> { }")]
    public void Alias_to_a_foreign_type_is_allowed(string source)
    {
        ResultLikeDeclarations(source).Should().BeEmpty();
    }

    [Fact(DisplayName = "Гейт не срабатывает на global using-псевдониме с чужой целью")]
    public void Global_alias_to_a_foreign_type_is_allowed()
    {
        const string usings = "global using Failure = Bugget.Entities.Reports.Failure;";
        const string declaration = "public sealed record Outcome<T>(T? Data, Failure? Error);";

        ResultLikeDeclarations(usings, declaration).Should().BeEmpty();
    }

    [Theory(DisplayName = "Параметр типа затеняет одноимённый using-псевдоним, как и в компиляторе")]
    [InlineData("using Failure = Bugget.Entities.Errors.Error; public sealed record Page<Failure>(string Data, Failure? Value);")]
    [InlineData("using Failure = Bugget.Entities.Errors.Error; public sealed class Page<Failure> : Choice<string, Failure> { }")]
    public void Type_parameter_shadowing_an_error_alias_is_allowed(string source)
    {
        ResultLikeDeclarations(source).Should().BeEmpty();
    }

    [Fact(DisplayName = "Псевдоним namespace перекрывает global using-псевдоним, как и в компиляторе")]
    public void Namespace_alias_wins_over_the_global_one()
    {
        const string usings = "global using Failure = Bugget.Entities.Errors.Error;";
        const string declaration =
            "namespace App; using Failure = Bugget.Entities.Reports.Failure; public sealed record Outcome<T>(T? Data, Failure? Error);";

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
            public sealed record TeamNotFoundError(string Code, string Title) : Error(Code, Title);

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

    /// <summary>
    /// Окружение fixtures: канонический <c>Error</c> с иерархией, посторонний тип того же
    /// имени, посторонний <c>Failure</c> и generic-базы, которыми пользуются fixtures.
    /// Объявления живут в отдельном файле компиляции и самим правилом не проверяются —
    /// проверяется ровно то, что передано в fixture.
    /// </summary>
    private const string FixtureContext = """
        global using Bugget.Entities.Errors;

        namespace Bugget.Entities.Errors
        {
            public abstract record Error(string Code, string Title);

            public sealed record BadRequestError(string Code, string Title) : Error(Code, Title);

            public sealed record NotFoundError(string Code, string Title) : Error(Code, Title);
        }

        namespace Bugget.Entities.Reports
        {
            public sealed record Failure(string Reason);
        }

        namespace ThirdParty
        {
            public sealed record Error(string Code, string Title);
        }

        namespace Contracts
        {
            public class Choice<TValue, TError>;
        }

        namespace Bugget.Contracts
        {
            public class Choice<TValue, TError>;
        }

        public class Choice<TValue, TError>;

        public class ErrorCarrier<TError>;
        """;

    /// <summary>
    /// Прогон правила по fixture: каждая строка — отдельный файл одного проекта, поэтому
    /// <c>global using</c> из одного файла виден остальным.
    ///
    /// Fixture обязан компилироваться: зелёный вердикт на коде, который не собирается,
    /// не доказывает ничего — связывание там не состоялось бы в любом случае.
    /// </summary>
    private static IReadOnlyList<string> ResultLikeDeclarations(params string[] sources)
    {
        var scanned = sources
            .Select((text, index) => ResultLikeTypes.Parse($"Fixture{index}.cs", text))
            .ToArray();
        var context = ResultLikeTypes.Parse("FixtureContext.cs", FixtureContext);

        var compilation = ResultLikeTypes.Compile(
            "Fixtures",
            [.. scanned.Select(source => source.Tree), context.Tree],
            ResultLikeTypes.RuntimeReferences);

        compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("fixture обязан компилироваться, иначе его вердикт ничего не значит");

        return [.. ResultLikeTypes.Find(scanned, compilation).Select(declaration => declaration.Type)];
    }

    /// <summary>Продуктовый проект: что проверяем и в какой компиляции связываем.</summary>
    private sealed record ProductProject(string Name, IReadOnlyList<ParsedSource> Scanned, CSharpCompilation Compilation);

    private static IReadOnlyList<ProductProject> ProductProjects => LazyProductProjects.Value;

    private static readonly Lazy<IReadOnlyList<ProductProject>> LazyProductProjects = new(LoadProductProjects);

    /// <summary>
    /// Компиляция на каждый продуктовый проект: свои деревья плюс сборки среды выполнения
    /// теста — в них уже лежат собранные соседние проекты, поэтому канонический <c>Error</c> и
    /// базовые типы связываются, а <c>global using</c> остаётся в границах проекта.
    ///
    /// Соседние проекты приходят именно сборками, а не второй компиляцией их исходников:
    /// один и тот же тип из двух ссылок становится неоднозначным, связывание отдаёт
    /// error-символ, и правило молча зеленеет. За тем, что канонический тип действительно
    /// связался, следит отдельный тест — молчащий гейт хуже отсутствующего.
    ///
    /// Проверяются не все деревья компиляции: сгенерированное из OpenAPI участвует в связывании
    /// как контекст, но правилу не предъявляется — его источник правды не в коде (ADR-0005).
    /// </summary>
    private static IReadOnlyList<ProductProject> LoadProductProjects()
    {
        var backend = SolutionGraph.BackendRoot;
        var projects = new List<(string Name, ParsedSource[] All, ParsedSource[] Scanned)>();

        foreach (var project in SolutionGraph.Projects.Values
                     .Where(project => !project.IsTestProject)
                     .OrderBy(project => project.Name, StringComparer.Ordinal))
        {
            var projectDir = Path.Combine(backend, project.Name);
            if (!Directory.Exists(projectDir))
            {
                continue;
            }

            var all = Directory
                .EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => ResultLikeTypes.Parse(Path.GetRelativePath(backend, path), File.ReadAllText(path)))
                .ToArray();

            if (all.Length == 0)
            {
                continue;
            }

            projects.Add((
                project.Name,
                all,
                [.. all.Where(source => !source.Path.EndsWith(".g.cs", StringComparison.Ordinal))]));
        }

        return
        [
            .. projects.Select(project => new ProductProject(
                project.Name,
                project.Scanned,
                ResultLikeTypes.Compile(project.Name, project.All.Select(source => source.Tree), ResultLikeTypes.RuntimeReferences)))
        ];
    }
}
