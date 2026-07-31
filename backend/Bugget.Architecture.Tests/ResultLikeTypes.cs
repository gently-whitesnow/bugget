using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bugget.Architecture.Tests;

/// <summary>Разобранный исходник: путь для диагностики и дерево для связывания.</summary>
internal sealed record ParsedSource(string Path, SyntaxTree Tree);

/// <summary>Объявленный тип, признанный Result-подобным: файл объявления и имя типа.</summary>
internal sealed record ResultLikeDeclaration(string File, string Type);

/// <summary>
/// Распознаёт Result-подобные объявленные типы — те, что совмещают каноническую ошибку
/// <c>Bugget.Entities.Errors.Error</c> с payload или признаком успеха (ADR-0004).
///
/// Тип определяется связыванием Roslyn, а не текстом имени. Разница принципиальная: имя
/// <c>Error</c> может принадлежать постороннему типу, канонический тип может приехать под
/// псевдонимом, а одноимённый параметр типа затеняет и то и другое. Правила видимости,
/// затенения и разрешения псевдонимов берутся у компилятора целиком — своей карты имён
/// здесь нет и заводить её нельзя: каждое исключение в ней ловится следующим
/// переименованием.
///
/// Единица связывания — компиляция проекта: <c>global using</c> действует на проект, и
/// собрать его из одного файла нельзя. Типы соседних проектов приходят ссылкой на
/// компиляцию всего backend, поэтому канонический <c>Error</c> виден отовсюду, даже если
/// архитектурные тесты на этот проект не ссылаются.
/// </summary>
internal static class ResultLikeTypes
{
    /// <summary>Каноническая ошибка решения: единственный тип, вокруг которого работает правило.</summary>
    internal const string CanonicalErrorMetadataName = "Bugget.Entities.Errors.Error";

    private const string CanonicalErrorDisplayName = "global::Bugget.Entities.Errors.Error";

    /// <summary>Имена, которыми Result-обёртка обычно называет признак успеха.</summary>
    private static readonly string[] SuccessFlagNames = ["IsSuccess", "IsFailure", "IsError", "HasError"];

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    private static readonly CSharpCompilationOptions CompilationOptions =
        new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);

    /// <summary>Сборки среды выполнения теста: без них не связывается даже <c>string</c>.</summary>
    internal static IReadOnlyList<MetadataReference> RuntimeReferences { get; } =
    [
        .. ((AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
    ];

    internal static ParsedSource Parse(string path, string text) =>
        new(path, CSharpSyntaxTree.ParseText(text, ParseOptions, path));

    internal static CSharpCompilation Compile(
        string assemblyName,
        IEnumerable<SyntaxTree> trees,
        IEnumerable<MetadataReference> references) =>
        CSharpCompilation.Create(assemblyName, trees, references, CompilationOptions);

    /// <summary>
    /// Result-подобные типы, объявленные в <paramref name="scanned"/>. Связывание идёт в
    /// <paramref name="compilation"/>, в которой эти деревья обязаны присутствовать: остальные
    /// её деревья и ссылки — контекст (сгенерированный код, соседние проекты), он не проверяется.
    /// </summary>
    internal static IReadOnlyList<ResultLikeDeclaration> Find(
        IReadOnlyList<ParsedSource> scanned,
        CSharpCompilation compilation)
    {
        var declarations = new List<ResultLikeDeclaration>();

        foreach (var source in scanned)
        {
            var model = compilation.GetSemanticModel(source.Tree);

            declarations.AddRange(source.Tree
                .GetRoot()
                .DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Where(declaration => IsResultLike(declaration, model))
                .Select(declaration => new ResultLikeDeclaration(source.Path, declaration.Identifier.ValueText)));
        }

        return declarations;
    }

    private static bool IsResultLike(TypeDeclarationSyntax declaration, SemanticModel model)
    {
        if (model.GetDeclaredSymbol(declaration) is not { } symbol)
        {
            return false;
        }

        // Ошибка бывает объявлена полем, свойством или позиционным параметром, а бывает
        // унаследована: `Outcome<T> : Choice<T, Error>` — та же обёртка, просто её половина
        // живёт в базовом типе. Цепочка баз обходится целиком: лишний слой наследования
        // ничего не меняет по смыслу.
        var inherited = InheritedTypeArguments(symbol);
        var carriesInheritedError = inherited.Any(IsCanonicalError);

        var dataMembers = DataMembers(declaration)
            .Select(member => (member.Name, Type: model.GetTypeInfo(member.Type).Type))
            .ToArray();
        var errorMembers = dataMembers.Where(member => IsCanonicalError(member.Type)).ToArray();

        if (errorMembers.Length == 0 && !carriesInheritedError)
        {
            return false;
        }

        // Тип, который несёт только ошибку, — это ещё не Result: Result появляется, когда
        // рядом с ошибкой лежит значение или признак успеха.
        var carriesPayload =
            dataMembers.Length > errorMembers.Length ||
            inherited.Any(argument => !IsCanonicalError(argument));

        return carriesPayload || dataMembers.Any(member => IsSuccessFlag(member.Name, member.Type));
    }

    /// <summary>
    /// Данные, которые несёт сам тип: позиционные параметры записи и нестатические поля и
    /// свойства. Статика исключена намеренно — справочник ошибок
    /// (<c>public static readonly NotFoundError …</c>) хранит ошибки, но ничего не заворачивает.
    /// Члены вложенных типов принадлежат вложенному типу, а не внешнему: перебираются только
    /// собственные члены объявления.
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
    /// Аргументы generic-баз по всей цепочке наследования: у <c>Outcome&lt;T&gt; :
    /// Choice&lt;T, Error&gt;</c> это T и Error. Сам базовый тип не важен — иначе под правило
    /// попала бы иерархия самих ошибок (<c>BadRequestError : Error</c>).
    /// </summary>
    private static IReadOnlyList<ITypeSymbol> InheritedTypeArguments(INamedTypeSymbol symbol)
    {
        var arguments = new List<ITypeSymbol>();

        for (var current = symbol.BaseType;
             current is not null && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType);
             current = current.BaseType)
        {
            arguments.AddRange(current.TypeArguments);
        }

        return arguments;
    }

    /// <summary>
    /// Тип связан именно с канонической ошибкой. Сравнение по полному имени, а не по ссылке:
    /// один и тот же тип приходит то из исходников своего проекта, то ссылкой на компиляцию
    /// соседних. Несвязанное имя (<c>TypeKind.Error</c>) и параметр типа каноническими не
    /// считаются — это и есть разница между «называется Error» и «является Error».
    /// </summary>
    private static bool IsCanonicalError(ITypeSymbol? type) =>
        type is INamedTypeSymbol { TypeKind: not TypeKind.Error } named &&
        named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == CanonicalErrorDisplayName;

    private static bool IsSuccessFlag(string name, ITypeSymbol? type) =>
        SuccessFlagNames.Contains(name, StringComparer.Ordinal) &&
        type?.SpecialType == SpecialType.System_Boolean;
}
