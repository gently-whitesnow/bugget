using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bugget.Architecture.Tests;

/// <summary>Объявленный тип, признанный Result-подобным: файл объявления и имя типа.</summary>
internal sealed record ResultLikeDeclaration(string File, string Type);

/// <summary>
/// Распознаёт Result-подобные объявленные типы — те, что совмещают каноническую ошибку
/// <c>Bugget.Entities.Errors.Error</c> с payload или признаком успеха (ADR-0004).
///
/// Что перед ним за тип, распознаватель спрашивает у компилятора: символ, полученный
/// связыванием, сравнивается с символом канонической ошибки той же компиляции. Сравнение
/// именно символов, а не имён: имя <c>Error</c> может принадлежать постороннему типу, тип с
/// тем же полным именем может приехать из чужой сборки, канонический тип может прийти под
/// псевдонимом, а одноимённый параметр типа затеняет и то и другое. Своей карты имён здесь
/// нет и заводить её нельзя: каждое исключение в ней ловится следующим переименованием.
///
/// Ошибка бывает не только полем: <c>Outcome&lt;T&gt; : Choice&lt;T, Error&gt;</c> и
/// <c>Outcome&lt;T&gt; : IChoice&lt;T, Error&gt;</c> — та же обёртка, просто её половина
/// живёт в базовом типе или в контракте. Поэтому аргументы ищутся по всей цепочке базовых
/// типов и по всем интерфейсам, включая generic-интерфейсы.
///
/// Типы самой иерархии ошибок под правило не попадают: <c>Error</c> и её наследники — это
/// ошибка, а не результат операции.
/// </summary>
internal static class ResultLikeTypes
{
    /// <summary>Каноническая ошибка решения: единственный тип, вокруг которого работает правило.</summary>
    internal const string CanonicalErrorMetadataName = "Bugget.Entities.Errors.Error";

    /// <summary>Сборка, которой обязана принадлежать каноническая ошибка.</summary>
    internal const string CanonicalErrorAssemblyName = "Bugget.Entities";

    /// <summary>Имена, которыми Result-обёртка обычно называет признак успеха.</summary>
    private static readonly string[] SuccessFlagNames = ["IsSuccess", "IsFailure", "IsError", "HasError"];

    /// <summary>
    /// Символ канонической ошибки в этой компиляции. Полного имени мало: тип с таким же
    /// именем может объявить любая сборка, поэтому проверяется и то, откуда он пришёл.
    /// </summary>
    internal static INamedTypeSymbol? CanonicalError(Compilation compilation) =>
        compilation
            .GetTypesByMetadataName(CanonicalErrorMetadataName)
            .FirstOrDefault(type => type.ContainingAssembly?.Name == CanonicalErrorAssemblyName);

    /// <summary>
    /// Сборка с канонической ошибкой доступна компиляции. Если нет — обёртку над этой ошибкой
    /// в проекте не объявить, и правилу там нечего искать: это гарантирует компилятор, а не
    /// умолчание правила.
    /// </summary>
    internal static bool SeesCanonicalErrors(Compilation compilation) =>
        compilation.Assembly.Name == CanonicalErrorAssemblyName ||
        compilation.SourceModule.ReferencedAssemblySymbols.Any(assembly => assembly.Name == CanonicalErrorAssemblyName);

    /// <summary>
    /// Result-подобные типы, объявленные в <paramref name="scanned"/>. Связывание идёт в
    /// <paramref name="compilation"/>, в которой эти деревья обязаны присутствовать: остальные
    /// её деревья и ссылки — контекст (сгенерированный код, соседние проекты), он не проверяется.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Сборка с канонической ошибкой подключена, а самого типа в компиляции нет. Молчать в
    /// этом случае нельзя: сравнение не совпало бы никогда и правило осталось бы зелёным на
    /// любом нарушении.
    /// </exception>
    internal static IReadOnlyList<ResultLikeDeclaration> Find(
        IReadOnlyList<ScannedFile> scanned,
        Compilation compilation)
    {
        if (!SeesCanonicalErrors(compilation))
        {
            return [];
        }

        var canonical = CanonicalError(compilation)
            ?? throw new InvalidOperationException(
                $"компиляции {compilation.AssemblyName} доступна сборка {CanonicalErrorAssemblyName}, " +
                $"но тип {CanonicalErrorMetadataName} в ней не связался: правилу не с чем сравнивать, " +
                "и оно осталось бы зелёным на любом нарушении");

        var declarations = new List<ResultLikeDeclaration>();

        foreach (var file in scanned)
        {
            var model = compilation.GetSemanticModel(file.Tree);

            declarations.AddRange(file.Tree
                .GetRoot()
                .DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Where(declaration => IsResultLike(declaration, model, canonical))
                .Select(declaration => new ResultLikeDeclaration(file.Path, declaration.Identifier.ValueText)));
        }

        return declarations;
    }

    private static bool IsResultLike(
        TypeDeclarationSyntax declaration,
        SemanticModel model,
        INamedTypeSymbol canonical)
    {
        if (model.GetDeclaredSymbol(declaration) is not { } symbol || IsError(symbol, canonical))
        {
            return false;
        }

        var carriers = InheritedErrorCarriers(symbol, canonical);
        var carriesInheritedError = carriers.Length > 0;

        var dataMembers = DataMembers(declaration)
            .Select(member => (member.Name, Type: model.GetTypeInfo(member.Type).Type))
            .ToArray();
        var errorMembers = dataMembers.Where(member => IsCanonical(member.Type, canonical)).ToArray();

        if (errorMembers.Length == 0 && !carriesInheritedError)
        {
            return false;
        }

        // Тип, который несёт только ошибку, — это ещё не Result: Result появляется, когда
        // рядом с ошибкой лежит значение или признак успеха. Значение бывает и своим членом,
        // и аргументом базового типа: `Outcome<T> : Choice<T, Error>` не объявляет ничего.
        var carriesPayload =
            dataMembers.Length > errorMembers.Length ||
            BaseTypes(symbol).SelectMany(type => type.TypeArguments).Any(argument => !IsCanonical(argument, canonical)) ||
            carriers.SelectMany(carrier => carrier.TypeArguments).Any(argument => !IsCanonical(argument, canonical));

        return carriesPayload || dataMembers.Any(member => IsSuccessFlag(member.Name, member.Type));
    }

    /// <summary>
    /// Базовые типы и интерфейсы, которые несут каноническую ошибку аргументом. Интерфейсы
    /// смотрятся только такие: у записи компилятор сам заводит <c>IEquatable&lt;TSelf&gt;</c>,
    /// и считать его аргумент payload'ом нельзя.
    /// </summary>
    private static INamedTypeSymbol[] InheritedErrorCarriers(INamedTypeSymbol symbol, INamedTypeSymbol canonical) =>
    [
        .. BaseTypes(symbol)
            .Concat(symbol.AllInterfaces)
            .Where(type => type.TypeArguments.Any(argument => IsCanonical(argument, canonical)))
    ];

    /// <summary>Цепочка базовых типов без <c>object</c> и <c>ValueType</c>.</summary>
    private static IEnumerable<INamedTypeSymbol> BaseTypes(INamedTypeSymbol symbol)
    {
        for (var current = symbol.BaseType;
             current is not null && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType);
             current = current.BaseType)
        {
            yield return current;
        }
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

    /// <summary>Сам тип и есть ошибка: канонический <c>Error</c> или его наследник.</summary>
    private static bool IsError(INamedTypeSymbol symbol, INamedTypeSymbol canonical) =>
        IsCanonical(symbol, canonical) || BaseTypes(symbol).Any(type => IsCanonical(type, canonical));

    private static bool IsCanonical(ITypeSymbol? type, INamedTypeSymbol canonical) =>
        SymbolEqualityComparer.Default.Equals(type, canonical);

    private static bool IsSuccessFlag(string name, ITypeSymbol? type) =>
        SuccessFlagNames.Contains(name, StringComparer.Ordinal) &&
        type?.SpecialType == SpecialType.System_Boolean;
}
