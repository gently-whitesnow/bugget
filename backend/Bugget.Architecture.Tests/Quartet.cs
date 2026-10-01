using System.Reflection;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Пять проектов целевой раскладки (ADR-0001). Якорь проекта — компилируемый <c>AssemblyMarker</c>:
/// переименование проекта ломает тесты сразу, а не молча выключает правило.
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
    /// Типы <c>Bugget.Api</c>, которым разрешено видеть <c>Bugget.Infrastructure</c>. Поимённый список, а не
    /// суффикс <c>*Extensions</c>: иначе любой новый класс с таким именем молча тянул бы инфраструктуру мимо портов.
    /// </summary>
    public static readonly string[] CompositionRoot =
    [
        "Bugget.Api.Extensions.ServiceCollectionExtensions",
        "Bugget.Api.Users.Extensions.ServiceCollectionExtensions",
        "Bugget.Api.Authorization.Extensions.ServiceCollectionExtensions",
    ];

    /// <summary>Ссылки сборки вне белого списка. Отдельная функция: её же прогоняет доказательство красноты.</summary>
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

    public static string[] ReferencesOf(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty)];
}
