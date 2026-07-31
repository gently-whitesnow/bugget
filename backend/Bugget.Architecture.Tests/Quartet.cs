using System.Reflection;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Пять проектов целевой раскладки (ADR-0001) и их сборки — одно место, куда смотрят
/// все правила. Якорь каждого проекта — его <c>AssemblyMarker</c>: ссылка компилируемая,
/// поэтому переименование проекта ломает тесты сразу, а не молча выключает правило.
/// </summary>
public static class Quartet
{
    public const string Domain = "Bugget.Domain";
    public const string Contracts = "Bugget.Contracts";
    public const string Application = "Bugget.Application";
    public const string Infrastructure = "Bugget.Infrastructure";
    public const string Api = "Bugget.Api";

    public static readonly Assembly DomainAsm = typeof(global::Bugget.Domain.AssemblyMarker).Assembly;
    public static readonly Assembly ContractsAsm = typeof(global::Bugget.Contracts.AssemblyMarker).Assembly;
    public static readonly Assembly ApplicationAsm = typeof(global::Bugget.Application.AssemblyMarker).Assembly;
    public static readonly Assembly InfrastructureAsm = typeof(global::Bugget.Infrastructure.AssemblyMarker).Assembly;
    public static readonly Assembly ApiAsm = typeof(global::Bugget.Api.AssemblyMarker).Assembly;

    /// <summary>
    /// Пространства имён композиционного корня: единственное место в <c>Bugget.Api</c>,
    /// которому разрешено видеть <c>Bugget.Infrastructure</c>. Всё остальное в Api
    /// разговаривает с инфраструктурой через порты прикладного слоя.
    /// </summary>
    public static readonly string[] CompositionRootTypeSuffixes = ["Extensions", "Program"];

    /// <summary>
    /// Имена сборок, на которые ссылается <paramref name="assembly"/> и которых нет
    /// в белом списке. Отдельная функция, а не тело теста: ту же проверку прогоняет
    /// доказательство красноты на синтетическом списке ссылок.
    /// </summary>
    public static string[] FindDisallowedReferences(
        string project,
        IEnumerable<string> referenced,
        IEnumerable<string> allowedPrefixes,
        IEnumerable<string>? allowedExact = null)
    {
        var prefixes = allowedPrefixes.ToArray();
        var exact = (allowedExact ?? []).ToHashSet(StringComparer.Ordinal);

        return
        [
            .. referenced
                .Where(name => !exact.Contains(name))
                .Where(name => !prefixes.Any(prefix =>
                    name.Equals(prefix, StringComparison.Ordinal)
                    || name.StartsWith(prefix + ".", StringComparison.Ordinal)))
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => $"{project} → {name}")
        ];
    }

    /// <summary>Имена сборок, на которые ссылается сборка проекта.</summary>
    public static string[] ReferencesOf(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty)];
}
