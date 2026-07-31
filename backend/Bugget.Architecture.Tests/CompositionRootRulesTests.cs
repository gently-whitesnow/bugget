using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Главное правило верхнего слоя (ADR-0001): <c>Bugget.Api</c> ссылается на
/// <c>Bugget.Infrastructure</c> только ради DI-композиции.
///
/// Композиционный корень опознаётся по типу, а не по каталогу: это <c>Program</c> и
/// классы <c>*Extensions</c>, которые собирают контейнер. Всё остальное в Api —
/// контроллеры, хабы, мапперы, фильтры — обязано разговаривать с внешним миром через
/// порты прикладного слоя, иначе транспорт снова знает про Npgsql и HTTP-клиенты.
///
/// Отступления текущего кода перечислены поимённо в
/// <see cref="KnownDeviations.ApiToInfrastructureTypes"/> и проверяются на протухание.
/// </summary>
public class CompositionRootRulesTests
{
    [Fact(DisplayName = "Bugget.Api видит Bugget.Infrastructure только из композиционного корня")]
    public void Only_composition_root_sees_infrastructure()
    {
        var violations = FindInfrastructureUsersOutsideCompositionRoot(Quartet.ApiAsm);

        var allowed = KnownDeviations.ApiToInfrastructureTypes
            .Select(deviation => deviation.From)
            .ToHashSet(StringComparer.Ordinal);

        violations
            .Where(type => !allowed.Contains(type))
            .Should().BeEmpty(
                "тип из Bugget.Api видит Bugget.Infrastructure, но композиционным корнем не является. " +
                "Контейнер собирают Program и классы *Extensions — им это разрешено; контроллеру, " +
                "хабу или мапперу нужен не тип инфраструктуры, а порт из Bugget.Application/Ports. " +
                "Нарушители: {0}. Текущие отступления — KnownDeviations.ApiToInfrastructureTypes.",
                string.Join(", ", violations));
    }

    [Fact(DisplayName = "Известные отступления Api → Infrastructure не протухли")]
    public void Known_api_deviations_are_still_real()
    {
        var actual = FindInfrastructureUsersOutsideCompositionRoot(Quartet.ApiAsm).ToHashSet(StringComparer.Ordinal);

        var stale = KnownDeviations.ApiToInfrastructureTypes
            .Where(deviation => !actual.Contains(deviation.From))
            .Select(deviation => deviation.ToString())
            .ToArray();

        stale.Should().BeEmpty(
            "отступление снято в коде, но осталось в списке KnownDeviations — вычеркни строку, " +
            "иначе список превращается в кладбище исключений и перестаёт означать долг. " +
            "Протухло: {0}",
            string.Join("; ", stale));
    }

    [Fact(DisplayName = "Правило композиционного корня доказуемо краснеет")]
    public void Composition_root_rule_is_provably_red()
    {
        // Прогоняем ту же функцию на сборке тестов, где заведён контроллер-нарушитель:
        // он ссылается на тип из Bugget.Infrastructure и композиционным корнем не является.
        FindInfrastructureUsersOutsideCompositionRoot(typeof(CompositionRootRulesTests).Assembly)
            .Should().Contain(typeof(CompositionFixtures.LeakingController).FullName!);
    }

    /// <summary>
    /// Типы сборки, которые зависят от <c>Bugget.Infrastructure</c> и при этом не являются
    /// композиционным корнем. Отдельная функция, а не тело теста: ту же проверку прогоняет
    /// доказательство красноты.
    /// </summary>
    private static string[] FindInfrastructureUsersOutsideCompositionRoot(Assembly assembly)
    {
        var result = Types
            .InAssembly(assembly)
            .That()
            .HaveDependencyOn(Quartet.Infrastructure)
            .GetTypes()
            .Select(type => type.FullName ?? type.Name)
            .Where(name => !IsCompositionRoot(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        return result;

        static bool IsCompositionRoot(string fullName)
        {
            var typeName = fullName.Split('.')[^1].Split('+')[0];
            return Quartet.CompositionRootTypeSuffixes.Any(suffix =>
                typeName.EndsWith(suffix, StringComparison.Ordinal));
        }
    }
}
