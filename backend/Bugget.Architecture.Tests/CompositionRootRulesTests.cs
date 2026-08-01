using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Главное правило верхнего слоя (ADR-0001): <c>Bugget.Api</c> ссылается на
/// <c>Bugget.Infrastructure</c> только ради DI-композиции.
///
/// Композиционный корень задан поимённым списком типов (<see cref="Quartet.CompositionRoot"/>),
/// а не соглашением об именовании: правило по суффиксу <c>*Extensions</c> раздавало бы
/// право тянуть инфраструктуру любому новому классу с таким именем. Всё остальное в Api —
/// контроллеры, хабы, мапперы, фильтры — разговаривает с внешним миром через порты
/// прикладного слоя.
/// </summary>
public class CompositionRootRulesTests
{
    [Fact(DisplayName = "Bugget.Api видит Bugget.Infrastructure только из композиционного корня")]
    public void Only_composition_root_sees_infrastructure()
    {
        var violations = FindInfrastructureUsersOutsideCompositionRoot(Quartet.ApiAsm);

        violations.Should().BeEmpty(
            "тип из Bugget.Api видит Bugget.Infrastructure, но в списке композиционного корня " +
            "его нет. Контейнер собирают перечисленные в Quartet.CompositionRoot типы; " +
            "контроллеру, хабу или мапперу нужен не тип инфраструктуры, а порт из " +
            "Bugget.Application/**/Ports. Нарушители: {0}. " +
            "Если это действительно новая точка сборки контейнера — добавь её в список " +
            "тем же коммитом, чтобы решение было видно в диффе.",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "Список композиционного корня не протух")]
    public void Composition_root_list_is_not_stale()
    {
        var apiTypes = Quartet.ApiAsm.GetTypes()
            .Select(type => type.FullName ?? type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missing = Quartet.CompositionRoot.Where(name => !apiTypes.Contains(name)).ToArray();

        missing.Should().BeEmpty(
            "тип из списка композиционного корня в Bugget.Api больше не существует — " +
            "вычеркни строку, иначе список перестаёт означать реальные точки сборки. " +
            "Протухло: {0}",
            string.Join(", ", missing));
    }

    [Fact(DisplayName = "Правило композиционного корня краснеет на контроллере с типом инфраструктуры")]
    public void Composition_root_rule_is_provably_red_for_a_controller()
    {
        // Прогоняем ту же функцию на сборке тестов, где заведён контроллер-нарушитель.
        FindInfrastructureUsersOutsideCompositionRoot(typeof(CompositionRootRulesTests).Assembly)
            .Should().Contain(typeof(CompositionFixtures.LeakingController).FullName!);
    }

    [Fact(DisplayName = "Правило композиционного корня краснеет на постороннем *Extensions")]
    public void Composition_root_rule_is_provably_red_for_a_foreign_extensions_class()
    {
        // Тот же прогон ловит класс с «правильным» именем, которого нет в списке: имя
        // права не даёт. Первая версия правила смотрела на суффикс и такой тип пропускала.
        FindInfrastructureUsersOutsideCompositionRoot(typeof(CompositionRootRulesTests).Assembly)
            .Should().Contain(typeof(CompositionFixtures.ForeignServiceCollectionExtensions).FullName!);
    }

    /// <summary>
    /// Типы сборки, которые зависят от <c>Bugget.Infrastructure</c> и при этом не входят
    /// в композиционный корень. Отдельная функция, а не тело теста: ту же проверку прогоняют
    /// доказательства красноты.
    /// </summary>
    private static string[] FindInfrastructureUsersOutsideCompositionRoot(Assembly assembly)
    {
        var compositionRoot = Quartet.CompositionRoot.ToHashSet(StringComparer.Ordinal);

        return
        [
            .. Types
                .InAssembly(assembly)
                .That()
                .HaveDependencyOn(Quartet.Infrastructure)
                .GetTypes()
                .Select(type => type.FullName ?? type.Name)
                .Where(name => !compositionRoot.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal)
        ];
    }
}
