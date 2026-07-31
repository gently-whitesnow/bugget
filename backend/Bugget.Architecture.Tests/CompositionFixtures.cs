namespace Bugget.Architecture.Tests.CompositionFixtures;

/// <summary>
/// Фикстура для доказательства красноты правила композиционного корня: «контроллер»,
/// который тянет тип инфраструктуры. Живёт в тестовой сборке и в продуктовый код не попадает.
/// </summary>
public sealed class LeakingController
{
    public global::Bugget.Infrastructure.AssemblyMarker Leak { get; } = new();
}
