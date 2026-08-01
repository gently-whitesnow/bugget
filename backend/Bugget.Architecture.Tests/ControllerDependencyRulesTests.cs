using FluentAssertions;

namespace Bugget.Architecture.Tests;

/// <summary>
/// HTTP-контроллеры зависят от границы use-case'ов Application, а не выбирают
/// конкретную реализацию. Это единый копируемый способ для users и reports-модулей.
/// Инфраструктурные порты дополнительно защищены правилом композиционного корня.
/// </summary>
public class ControllerDependencyRulesTests
{
    [Fact(DisplayName = "Контроллеры получают application-сервисы только через интерфейсы")]
    public void Controllers_depend_on_application_service_interfaces()
    {
        var controllers = Quartet.ApiAsm.GetTypes()
            .Where(type => !type.IsAbstract)
            .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal));

        var violations = FindConcreteApplicationDependencies(controllers);

        violations.Should().BeEmpty(
            "контроллер — транспортный адаптер: граница use-case'ов задаётся интерфейсом " +
            "Bugget.Application, а реализация выбирается только в DI-композиции. Нарушители: {0}",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "Правило DI контроллеров краснеет на concrete application-сервисе")]
    public void Controller_dependency_rule_is_provably_red()
    {
        FindConcreteApplicationDependencies([typeof(ConcreteApplicationServiceConsumer)])
            .Should().ContainSingle()
            .Which.Should().EndWith("→ Bugget.Application.Services.Reports.ReportsService");
    }

    private static string[] FindConcreteApplicationDependencies(IEnumerable<Type> types) =>
    [
        .. types
            .SelectMany(type => type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => parameter.ParameterType.Assembly == Quartet.ApplicationAsm)
                .Where(parameter => !parameter.ParameterType.IsInterface)
                .Select(parameter => $"{type.FullName} → {parameter.ParameterType.FullName}"))
            .OrderBy(value => value, StringComparer.Ordinal)
    ];

    private sealed record ConcreteApplicationServiceConsumer(
        global::Bugget.Application.Services.Reports.ReportsService Service);
}
