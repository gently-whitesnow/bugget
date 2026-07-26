using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace Bugget.Tests.Architecture;

/// <summary>
/// Соглашения именования и контракты для агента, который генерит код в bugget-api.
///
/// Цель — чтобы расположение типа можно было предсказать по его имени, а у
/// каждого DbClient'а был интерфейс — это открывает mock'ание персистенса в
/// BO-unit-тестах без реальной БД.
/// </summary>
public class NamingConventionRulesTests
{
    private static readonly Assembly ApiAsm = typeof(global::Bugget.AssemblyMarker).Assembly;
    private static readonly Assembly BoAsm = typeof(global::Bugget.BO.AssemblyMarker).Assembly;
    private static readonly Assembly DaAsm = typeof(global::Bugget.DA.AssemblyMarker).Assembly;

    private const string DaInterfacesNamespace = "Bugget.DA.Interfaces";

    [Fact(DisplayName = "*Service в Bugget.BO живёт в namespace Bugget.BO.Services.*")]
    public void Service_classes_must_reside_in_BO_Services_namespace()
    {
        // Сервис — единица оркестрации бизнес-логики. Если хочется *Service вне
        // Bugget.BO.Services — либо это не сервис (переименуй), либо его место в Services.
        var result = Types
            .InAssembly(BoAsm)
            .That()
            .HaveNameEndingWith("Service")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveName("BackgroundService") // абстрактный helper из BCL, если попадёт
            .Should()
            .ResideInNamespaceStartingWith("Bugget.BO.Services")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Сервис обязан жить в Bugget.BO/Services/**. " +
            "Если это не сервис в смысле BO — переименуй, чтобы суффикс не вводил в заблуждение. " +
            "Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact(DisplayName = "*DbClient живёт в namespace Bugget.DA.Postgres.*")]
    public void DbClient_classes_must_reside_in_DA_Postgres_namespace()
    {
        // *DbClient — это Dapper/Npgsql клиент к Postgres. Если появился DbClient вне
        // Bugget.DA/Postgres — это либо неудачное имя (тогда переименуй), либо
        // нарушение слоистости (тогда перенеси в Bugget.DA/Postgres).
        var result = Types
            .InAssembly(DaAsm)
            .That()
            .HaveNameEndingWith("DbClient")
            .And()
            .AreClasses()
            .Should()
            .ResideInNamespaceStartingWith("Bugget.DA.Postgres")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "*DbClient обязан жить в Bugget.DA/Postgres/**. " +
            "Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact(DisplayName = "*Controller в Bugget наследуется от Bugget.Controllers.ApiController")]
    public void Controllers_must_inherit_from_ApiController()
    {
        // ApiController — общая база, поверх которой навешан [ApiController] и shared filter pipeline.
        // Голый ControllerBase в проекте — это либо забытое наследование, либо самопальная HTTP-точка.
        //
        // Контроллеры, унаследованные от NSwag-сгенерированного *ControllerBase
        // (живёт в Bugget.Api.Generated.*), не наследуются от ApiController напрямую —
        // их базой управляет codegen, и менять её через рукописный класс нельзя.
        // Глобальный `[ApiController]` приходит из `AddControllers` policy + ControllerBase.
        var result = Types
            .InAssembly(ApiAsm)
            .That()
            .HaveNameEndingWith("Controller")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveName("ApiController") // сам base
            .And()
            .DoNotResideInNamespaceStartingWith("Bugget.Api.Generated")
            .Should()
            .Inherit(typeof(global::Bugget.Controllers.ApiController))
            .GetResult();

        var generatedBased = ApiAsm.GetTypes()
            .Where(t => t.IsClass && t.Name.EndsWith("Controller"))
            .Where(t => t.BaseType != null
                        && t.BaseType.Namespace != null
                        && t.BaseType.Namespace.StartsWith("Bugget.Api.Generated"))
            .Select(t => t.FullName ?? t.Name)
            .ToArray();

        var failing = (result.FailingTypeNames ?? []).Except(generatedBased).ToArray();

        failing.Should().BeEmpty(
            "*Controller обязан наследоваться от Bugget.Controllers.ApiController либо " +
            "от сгенерированного *ControllerBase в Bugget.Api.Generated.*. " +
            "Failing types: {0}",
            string.Join(", ", failing));
    }

    [Fact(DisplayName = "Все *DbClient в Bugget.DA.Postgres реализуют интерфейс из Bugget.DA.Interfaces")]
    public void All_DbClients_implement_interface_from_DA_Interfaces()
    {
        // Каждый *DbClient должен иметь I*DbClient в Bugget.DA.Interfaces, чтобы BO-сервисы
        // могли его подменить через DI и mock'нуть в unit-тестах. Если интерфейса нет — заведи.
        var violations = DaAsm.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("Bugget.DA.Postgres"))
            .Where(t => t.Name.EndsWith("DbClient"))
            .Where(t => !t.GetInterfaces().Any(i =>
                i.Namespace == DaInterfacesNamespace && !i.IsGenericType))
            .Select(t => t.FullName ?? t.Name)
            .ToArray();

        violations.Should().BeEmpty(
            "Каждый *DbClient в Bugget.DA/Postgres должен реализовывать интерфейс I*DbClient " +
            $"из {DaInterfacesNamespace}. Без интерфейса BO-сервис нельзя протестировать без БД. " +
            "Заведи I*DbClient рядом и подключи через ': IFooDbClient'. " +
            "Failing types: {0}",
            string.Join(", ", violations));
    }
}
