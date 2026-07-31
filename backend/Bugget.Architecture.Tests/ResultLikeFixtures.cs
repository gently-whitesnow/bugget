using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Окружение для fixtures правила Result-подобных типов: канонические ошибки лежат в сборке
/// <c>Bugget.Entities</c>, посторонние типы — в других сборках. Это не модель продуктового
/// проекта — тот открывается через MSBuild (<see cref="BackendSolution"/>); здесь собирается
/// ровно тот код, который fixture предъявляет правилу.
///
/// Разделение по сборкам нужно самому правилу: каноническая ошибка определяется не полным
/// именем, а символом из <c>Bugget.Entities</c>, и проверить это можно только чужой сборкой,
/// объявляющей тип с тем же именем.
/// </summary>
internal static class ResultLikeFixtures
{
    /// <summary>Канонические ошибки: сборка называется так же, как продуктовая.</summary>
    private const string CanonicalErrorsSource = """
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
        """;

    /// <summary>Чужая сборка с тем же полным именем типа: каноническим он от этого не становится.</summary>
    private const string ForeignErrorsSource = """
        namespace Bugget.Entities.Errors
        {
            public abstract record Error(string Code, string Title);
        }
        """;

    /// <summary>Базы, контракты и посторонний тип с именем <c>Error</c>, которыми пользуются fixtures.</summary>
    private const string ContractsSource = """
        public class Choice<TValue, TError>;

        public interface IChoice<TValue, TError>;

        public class ErrorCarrier<TError>;

        namespace Contracts
        {
            public class Choice<TValue, TError>;
        }

        namespace Bugget.Contracts
        {
            public class Choice<TValue, TError>;
        }

        namespace ThirdParty
        {
            public sealed record Error(string Code, string Title);
        }
        """;

    /// <summary>Чтобы в fixtures работало короткое имя <c>Error</c>, как в продуктовом коде.</summary>
    private const string ImportsSource = "global using Bugget.Entities.Errors;";

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    private static readonly CSharpCompilationOptions CompilationOptions =
        new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);

    /// <summary>
    /// Только сборки платформы: продуктовые DLL сюда попасть не должны — иначе канонический
    /// <c>Error</c> окажется объявлен дважды и fixtures перестанут компилироваться.
    /// </summary>
    private static readonly IReadOnlyList<MetadataReference> FrameworkReferences =
    [
        .. ((AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(path => IsFrameworkAssembly(Path.GetFileName(path)))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
    ];

    private static readonly Lazy<MetadataReference> CanonicalErrors =
        new(() => Reference(ResultLikeTypes.CanonicalErrorAssemblyName, CanonicalErrorsSource));

    private static readonly Lazy<MetadataReference> ForeignErrors =
        new(() => Reference("Foreign.Sdk", ForeignErrorsSource));

    private static readonly Lazy<MetadataReference> Contracts =
        new(() => Reference("Fixtures.Contracts", ContractsSource));

    /// <summary>
    /// Прогон правила по fixture: каждая строка — отдельный файл одного проекта, поэтому
    /// <c>global using</c> из одного файла виден остальным.
    /// </summary>
    internal static IReadOnlyList<string> ResultLikeDeclarations(params string[] sources) =>
        Analyze(sources, canonicalErrors: true);

    /// <summary>То же, но полное имя канонической ошибки объявляет чужая сборка.</summary>
    internal static IReadOnlyList<string> ResultLikeDeclarationsWithForeignErrors(params string[] sources) =>
        Analyze(sources, canonicalErrors: false);

    /// <summary>Компиляция fixture — она же нужна тестам, которые смотрят на канонический символ.</summary>
    internal static CSharpCompilation CompileFixture(IReadOnlyList<string> sources, bool canonicalErrors = true)
    {
        var trees = sources
            .Select((text, index) => CSharpSyntaxTree.ParseText(text, ParseOptions, $"Fixture{index}.cs"))
            .Append(CSharpSyntaxTree.ParseText(ImportsSource, ParseOptions, "FixtureImports.cs"));

        var compilation = CSharpCompilation.Create(
            "Fixtures",
            trees,
            [.. FrameworkReferences, Contracts.Value, canonicalErrors ? CanonicalErrors.Value : ForeignErrors.Value],
            CompilationOptions);

        // Fixture обязан компилироваться: зелёный вердикт на коде, который не собирается,
        // не доказывает ничего — связывание там не состоялось бы в любом случае.
        compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("fixture обязан компилироваться, иначе его вердикт ничего не значит");

        return compilation;
    }

    private static IReadOnlyList<string> Analyze(IReadOnlyList<string> sources, bool canonicalErrors)
    {
        var compilation = CompileFixture(sources, canonicalErrors);

        var scanned = compilation.SyntaxTrees
            .Where(tree => tree.FilePath != "FixtureImports.cs")
            .Select(tree => new ScannedFile(tree.FilePath, tree))
            .ToArray();

        return [.. ResultLikeTypes.Find(scanned, compilation).Select(declaration => declaration.Type)];
    }

    private static MetadataReference Reference(string assemblyName, string source) =>
        CSharpCompilation
            .Create(assemblyName, [CSharpSyntaxTree.ParseText(source, ParseOptions)], FrameworkReferences, CompilationOptions)
            .ToMetadataReference();

    private static bool IsFrameworkAssembly(string fileName) =>
        fileName.StartsWith("System.", StringComparison.Ordinal) ||
        fileName is "netstandard.dll" or "mscorlib.dll" or "System.dll";
}
