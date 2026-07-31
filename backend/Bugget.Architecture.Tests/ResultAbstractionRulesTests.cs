using FluentAssertions;
using Microsoft.CodeAnalysis;
using static Bugget.Architecture.Tests.ResultLikeFixtures;

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
/// Правило смотрит на исходники всего решения, а не на те проекты, на которые ссылается сам
/// тест: новый проект попадает под него в день появления. Что перед ним за тип, оно
/// спрашивает у компилятора — см. <see cref="ResultLikeTypes"/> и <see cref="BackendSolution"/>.
/// </summary>
public class ResultAbstractionRulesTests
{
    private static readonly string[] ForbiddenProjectNames = ["Monade", "Flow"];

    [Fact(DisplayName = "Новых Result-подобных обёрток нет: успех и ошибка не заворачиваются в тип")]
    public async Task No_result_like_wrapper_types()
    {
        var projects = await BackendSolution.ProductProjectsAsync();

        var violations = projects
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

    [Fact(DisplayName = "Компиляции проектов связываются без ошибок: иначе правило слепнет")]
    public async Task Product_compilations_bind_without_errors()
    {
        var projects = await BackendSolution.ProductProjectsAsync();

        var broken = projects
            .SelectMany(project => project.Compilation
                .GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Take(3)
                .Select(diagnostic => $"{project.Name}: {diagnostic}"))
            .ToArray();

        broken.Should().BeEmpty(
            "правило смотрит на связанные типы. Несвязанный тип становится error-символом: " +
            "база обёртки перестаёт быть базой, и нарушение проходит молча. Ошибки связывания: {0}.",
            string.Join("; ", broken));
    }

    /// <summary>
    /// Проекты, которые обязаны видеть каноническую ошибку: именно они возвращали Result-монады
    /// до ADR-0004, и именно на них правило обязано работать. Проект, потерявший её из виду,
    /// правило молча пропустит.
    /// </summary>
    private static readonly string[] ProjectsThatReturnErrors =
    [
        "Authorization.Api", "Bugget", "Bugget.BO", "Bugget.DA", "Bugget.Entities", "Bugget.Http",
        "Users.Api", "Users.BO", "Users.DA"
    ];

    [Fact(DisplayName = "Каноническая ошибка приходит из сборки Bugget.Entities всюду, где она вообще доступна")]
    public async Task Canonical_error_type_is_bound_where_its_assembly_is_referenced()
    {
        var projects = await BackendSolution.ProductProjectsAsync();

        var blind = projects
            .Where(project => ResultLikeTypes.SeesCanonicalErrors(project.Compilation))
            .Where(project => ResultLikeTypes.CanonicalError(project.Compilation) is null)
            .Select(project => project.Name)
            .ToArray();

        blind.Should().BeEmpty(
            "правило сравнивает связанный тип с символом {0} из сборки {1}. Если сборка подключена, " +
            "а символ не связался, сравнение не совпадёт никогда и гейт замолчит, оставаясь " +
            "зелёным. Проекты, в которых канонический тип не связался: {2}.",
            ResultLikeTypes.CanonicalErrorMetadataName,
            ResultLikeTypes.CanonicalErrorAssemblyName,
            string.Join(", ", blind));

        var unreachable = ProjectsThatReturnErrors
            .Except(projects
                .Where(project => ResultLikeTypes.CanonicalError(project.Compilation) is not null)
                .Select(project => project.Name))
            .ToArray();

        unreachable.Should().BeEmpty(
            "проекты, которые возвращают ошибки, обязаны видеть канонический тип — иначе правило " +
            "пропускает их целиком и молчит. Не видят: {0}.",
            string.Join(", ", unreachable));
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

    [Theory(DisplayName = "Гейт краснеет на generic Result-обёртке, собранной наследованием, при любой квалификации имени")]
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

    [Theory(DisplayName = "Generic-интерфейс — такая же Result-подобная форма, как базовый класс")]
    [InlineData("public sealed class Outcome<T> : IChoice<T, Error> { }")]
    [InlineData("public sealed record Outcome<T> : IChoice<T, Error>;")]
    public void Result_like_generic_interface_is_rejected(string source)
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

    [Fact(DisplayName = "Тип с тем же полным именем из чужой сборки каноническим не считается")]
    public void Foreign_assembly_with_the_canonical_metadata_name_is_not_canonical()
    {
        const string source = "public sealed record Outcome<T>(T? Data, Bugget.Entities.Errors.Error? Error);";

        // Тот же исходник со сборкой Bugget.Entities правило считает нарушением — значит
        // разница ровно в том, откуда пришёл символ, а не в форме типа.
        ResultLikeDeclarations(source).Should().Equal("Outcome");

        ResultLikeTypes
            .CanonicalError(CompileFixture([source], canonicalErrors: false))
            .Should().BeNull("канонической ошибку делает сборка Bugget.Entities, а не полное имя типа");
    }

    [Fact(DisplayName = "Обёртка над чужой ошибкой того же имени нарушением не считается")]
    public void Wrapper_over_a_foreign_error_type_is_allowed()
    {
        const string source = "public sealed record Outcome<T>(T? Data, Bugget.Entities.Errors.Error? Error);";

        ResultLikeDeclarationsWithForeignErrors(source).Should().BeEmpty(
            "канонической ошибку делает сборка Bugget.Entities: тип из чужой сборки — это чужой тип");
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

    [Fact(DisplayName = "Гейт оставляет допустимой запись, которая несёт только ошибку")]
    public void Record_carrying_only_an_error_is_allowed()
    {
        const string source = "public sealed record FailureState(Error? Error);";

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
}
