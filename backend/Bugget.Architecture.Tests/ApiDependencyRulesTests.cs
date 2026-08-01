using System.Reflection;
using FluentAssertions;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Любой тип <c>Bugget.Api</c> — контроллер, хаб, фильтр, маппер, фоновый сервис —
/// получает use-case прикладного слоя через интерфейс, объявленный в <c>Bugget.Application</c>,
/// а не через конкретную реализацию. Правило смотрит на всю сборку, а не на суффикс
/// <c>*Controller</c>: суффикс — соглашение об именовании, и новый хаб с concrete-сервисом
/// в конструкторе проходил бы мимо гейта.
///
/// Единственное исключение — поимённый композиционный корень
/// (<see cref="Quartet.CompositionRoot"/>), тот же список, что и в
/// <see cref="CompositionRootRulesTests"/>: выбор реализации — работа этих типов.
/// Ни суффикс, ни namespace исключения не дают.
/// </summary>
public class ApiDependencyRulesTests
{
    [Fact(DisplayName = "Типы Bugget.Api получают application-сервисы только через интерфейсы")]
    public void Api_types_depend_on_application_service_interfaces()
    {
        var violations = FindConcreteApplicationDependencies(Quartet.ApiAsm, Quartet.CompositionRoot);

        violations.Should().BeEmpty(
            "тип из Bugget.Api принимает в конструкторе конкретный тип Bugget.Application. " +
            "Транспорт — адаптер: границу use-case'а задаёт интерфейс, объявленный рядом " +
            "с реализацией в Bugget.Application, а конкретный класс выбирает только " +
            "DI-композиция. Замени параметр на этот application-контракт; если интерфейса " +
            "ещё нет — заведи его рядом с реализацией. Если это действительно новая точка " +
            "сборки контейнера, добавь тип в Quartet.CompositionRoot тем же коммитом. " +
            "Нарушители (тип Api → конкретный тип Application): {0}",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "Правило DI Api краснеет на хабе с concrete application-сервисом")]
    public void Api_dependency_rule_is_provably_red_for_a_hub()
    {
        // Прогоняем ту же функцию на сборке тестов, где заведён хаб-нарушитель. Его имя
        // не заканчивается на Controller: до расширения правило такой тип не видело.
        FindConcreteApplicationDependencies(typeof(ApiDependencyRulesTests).Assembly, [])
            .Should().Contain(
                $"{typeof(CompositionFixtures.LeakingReportPageHub).FullName} → " +
                "Bugget.Application.Services.Reports.ReportsService");
    }

    [Fact(DisplayName = "Правило DI Api краснеет на Api-адаптере с любым именем")]
    public void Api_dependency_rule_is_provably_red_regardless_of_type_name()
    {
        // Второй нарушитель без узнаваемого суффикса вообще: правило смотрит на сборку
        // и конструктор, а не на имя типа.
        FindConcreteApplicationDependencies(typeof(ApiDependencyRulesTests).Assembly, [])
            .Should().Contain(
                $"{typeof(CompositionFixtures.LeakingRealtimePublisher).FullName} → " +
                "Bugget.Application.Services.Reports.ReportsService");
    }

    [Fact(DisplayName = "Поимённый композиционный корень вправе брать конкретные реализации")]
    public void Composition_root_is_allowed_to_take_concrete_implementations()
    {
        var compositionRoot = typeof(CompositionFixtures.WiringCompositionRoot).FullName!;

        // Сначала — что фикстура вообще нарушитель: иначе «зелёность» ничего не доказывает.
        FindConcreteApplicationDependencies(typeof(ApiDependencyRulesTests).Assembly, [])
            .Should().Contain(name => name.StartsWith(compositionRoot, StringComparison.Ordinal));

        // И что зелёной её делает именно поимённое исключение, а не форма или имя типа.
        FindConcreteApplicationDependencies(typeof(ApiDependencyRulesTests).Assembly, [compositionRoot])
            .Should().NotContain(name => name.StartsWith(compositionRoot, StringComparison.Ordinal));
    }

    /// <summary>
    /// Пары «тип сборки → конкретный тип <c>Bugget.Application</c> в его конструкторе»
    /// для всех неабстрактных типов, кроме перечисленных в <paramref name="compositionRoot"/>.
    /// Отдельная функция, а не тело теста: ту же проверку прогоняют доказательства
    /// красноты и зелёности.
    /// </summary>
    private static string[] FindConcreteApplicationDependencies(
        Assembly assembly,
        IEnumerable<string> compositionRoot)
    {
        var allowed = compositionRoot.ToHashSet(StringComparer.Ordinal);

        return
        [
            .. assembly.GetTypes()
                .Where(type => !type.IsAbstract)
                .Where(type => !allowed.Contains(type.FullName ?? type.Name))
                .SelectMany(type => type.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Where(parameter => parameter.ParameterType.Assembly == Quartet.ApplicationAsm)
                    .Where(parameter => !parameter.ParameterType.IsInterface)
                    .Select(parameter => $"{type.FullName} → {parameter.ParameterType.FullName}"))
                .OrderBy(value => value, StringComparer.Ordinal)
        ];
    }
}
