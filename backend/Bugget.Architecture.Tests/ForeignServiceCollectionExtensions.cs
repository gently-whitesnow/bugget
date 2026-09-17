namespace Bugget.Architecture.Tests.CompositionFixtures;

/// <summary>
/// Вторая фикстура: тип с «правильным» суффиксом <c>*Extensions</c>, которого нет в списке
/// композиционного корня. Проверяет, что правило смотрит в список, а не на имя, — именно
/// так соглашение об именовании превращалось бы в дыру.
/// </summary>
public static class ForeignServiceCollectionExtensions
{
    public static global::Bugget.Infrastructure.AssemblyMarker Leak() => new();
}
