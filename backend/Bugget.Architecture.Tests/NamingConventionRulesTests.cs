using FluentAssertions;
using NetArchTest.Rules;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Соглашения именования квартета: по имени типа должно быть понятно, в каком слое он живёт.
///
/// Цель — чтобы расположение типа предсказывалось по имени, а у каждого адаптера персистенса
/// был порт: это и есть то, что открывает тестирование прикладного слоя без БД.
/// </summary>
public class NamingConventionRulesTests
{
    private const string ServicesNamespace = "Bugget.Application.Services";
    private const string PostgresNamespace = "Bugget.Infrastructure.Postgres";

    [Fact(DisplayName = "*Service прикладного слоя живёт в Bugget.Application.Services.* или .Users.*")]
    public void Services_reside_in_application_services_namespace()
    {
        // Сервис — единица оркестрации прикладного слоя. Модуль users сохранил свою
        // раскладку внутри Bugget.Application.Users — это тот же прикладной слой.
        var violations = Quartet.ApplicationAsm.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.Name.EndsWith("Service", StringComparison.Ordinal))
            .Where(type => type.Namespace is not { } ns
                           || !(ns.StartsWith(ServicesNamespace, StringComparison.Ordinal)
                                || ns.StartsWith("Bugget.Application.Users", StringComparison.Ordinal)))
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "сервис обязан жить в Bugget.Application/Services/** (или в Bugget.Application/Users/** " +
            "для модуля users). Если это не сервис в прикладном смысле — переименуй, чтобы суффикс " +
            "не вводил в заблуждение. Нарушители: {0}",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "*DbClient живёт в Bugget.Infrastructure.*")]
    public void DbClients_reside_in_infrastructure()
    {
        // *DbClient — это Dapper/Npgsql клиент к Postgres. DbClient вне инфраструктуры —
        // либо неудачное имя, либо нарушение слоистости.
        var strayLayers = new[] { Quartet.ApplicationAsm, Quartet.DomainAsm, Quartet.ContractsAsm, Quartet.ApiAsm }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && type.Name.EndsWith("DbClient", StringComparison.Ordinal))
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        strayLayers.Should().BeEmpty(
            "*DbClient — адаптер персистенса и живёт только в Bugget.Infrastructure. Нарушители: {0}",
            string.Join(", ", strayLayers));
    }

    [Fact(DisplayName = "Все *DbClient реализуют порт из *.Ports прикладного слоя")]
    public void DbClients_implement_application_port()
    {
        // Порт объявляет прикладной слой, инфраструктура его реализует (ADR-0001). Без порта
        // сервис нельзя подменить через DI и протестировать без БД.
        var violations = Quartet.InfrastructureAsm.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.Name.EndsWith("DbClient", StringComparison.Ordinal))
            .Where(type => !type.GetInterfaces().Any(IsApplicationPort))
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty(
            "каждый *DbClient обязан реализовывать порт из Bugget.Application/**/Ports. Без порта " +
            "прикладной слой нельзя протестировать без БД. Заведи интерфейс рядом с вызывающим " +
            "кодом и подключи через ': IFooDbClient'. Нарушители: {0}",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "*Controller в Bugget.Api наследует ApiController или сгенерированную базу")]
    public void Controllers_inherit_api_base()
    {
        // ApiController — общая база, поверх которой навешан [ApiController] и общий filter
        // pipeline. Голый ControllerBase — это либо забытое наследование, либо самопальная точка.
        // Контроллеры, унаследованные от NSwag-сгенерированного *ControllerBase
        // (Bugget.Api.Generated.*), базой не управляют: её задаёт codegen (ADR-0005).
        var result = Types
            .InAssembly(Quartet.ApiAsm)
            .That()
            .HaveNameEndingWith("Controller")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveName("ApiController")
            .And()
            .DoNotResideInNamespaceStartingWith("Bugget.Api.Generated")
            .Should()
            .Inherit(typeof(global::Bugget.Api.Controllers.ApiController))
            .GetResult();

        var generatedBased = Quartet.ApiAsm.GetTypes()
            .Where(type => type.IsClass && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Where(type => type.BaseType?.Namespace?.StartsWith("Bugget.Api.Generated", StringComparison.Ordinal) == true)
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        // Модули users, authorization, oidc и fake живут на собственных базах ASP.NET:
        // их контроллеры приехали отдельными сервисами и общего фильтра Bugget не знают.
        var moduleControllers = Quartet.ApiAsm.GetTypes()
            .Where(type => type.IsClass && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Where(type => type.Namespace is { } ns
                           && (ns.StartsWith("Bugget.Api.Users", StringComparison.Ordinal)
                               || ns.StartsWith("Bugget.Api.Authorization", StringComparison.Ordinal)))
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        var failing = (result.FailingTypeNames ?? [])
            .Except(generatedBased)
            .Except(moduleControllers)
            .ToArray();

        failing.Should().BeEmpty(
            "*Controller обязан наследоваться от Bugget.Api.Controllers.ApiController либо от " +
            "сгенерированного *ControllerBase в Bugget.Api.Generated.*. Нарушители: {0}",
            string.Join(", ", failing));
    }

    private static bool IsApplicationPort(Type contract) =>
        contract.Namespace is { } ns
        && ns.StartsWith("Bugget.Application", StringComparison.Ordinal)
        && ns.EndsWith(".Ports", StringComparison.Ordinal)
        && !contract.IsGenericType;
}
