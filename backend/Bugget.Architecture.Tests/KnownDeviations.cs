namespace Bugget.Architecture.Tests;

/// <summary>
/// Реестр отступлений кода от целевой раскладки ADR-0001; сейчас пуст. Это долг, а не разрешение:
/// пополнение обосновывается как отключение гейта (ADR-0002), нормальное движение — вычёркивание.
/// Каждая запись проверяется на протухание: отступление снято в коде — строка обязана исчезнуть, иначе гейт краснеет.
/// </summary>
public static class KnownDeviations
{
    /// <summary>Рёбра «прикладной слой → инфраструктура», которые сейчас есть в графе проектов.</summary>
    public static readonly IReadOnlyList<Deviation> ApplicationProjectReferences = [];

    /// <summary>Сборки, которые прикладной слой тянет напрямую в обход целевого правила.</summary>
    public static readonly IReadOnlyList<Deviation> ApplicationAssemblyReferences = [];

    /// <summary>Все отступления одним списком — для сообщений и проверки на протухание.</summary>
    public static IReadOnlyList<Deviation> All =>
    [
        .. ApplicationProjectReferences,
        .. ApplicationAssemblyReferences,
    ];

    /// <summary>Разрешённые цели отступлений для проекта — то, что правило обязано пропустить.</summary>
    public static IReadOnlyCollection<string> TargetsFor(IReadOnlyList<Deviation> deviations, string project) =>
        [.. deviations.Where(d => d.From == project).Select(d => d.To)];
}
