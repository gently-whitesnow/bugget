using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Архитектурные правила для bugget-api (см. ROOT.md → «Карта проекта», «Инварианты»).
///
/// Разрешённое направление зависимостей (ADR-0001):
///   Bugget (API) ──► Bugget.BO ──► Bugget.Entities
///   Bugget (API) ──► Bugget.DA ──► Bugget.BO.Ports   (только ради DI-композиции)
///                    TaskQueue ──► (BCL)
///   Bugget ──► Bugget.DbUp, Bugget.ExternalClients
///   Bugget.ExternalClients ──► Bugget.BO
///
/// Главное правило: «слои не пересекаются назад». Порты объявляет бизнес-логика
/// (Bugget.BO/Ports), инфраструктура их реализует — поэтому единственное, что
/// Bugget.DA видит из Bugget.BO, это namespace портов.
///
/// Технический момент: NetArchTest сравнивает namespace по строковому startsWith,
/// поэтому `NotHaveDependencyOn("Bugget")` поймает и сам Bugget.DA. Для API-слоя
/// перечисляем конкретные namespace'ы (Bugget.Controllers, Bugget.Hubs и т.д.),
/// для проектов-листьев используем whitelist через OnlyHaveDependenciesOn.
/// </summary>
public class LayerDependencyRulesTests
{
    // Конкретные namespace'ы внутри Bugget (API) — не коллизят с Bugget.DA / Bugget.BO
    // при NetArchTest-овском startsWith-сравнении.
    private static readonly string[] ApiInternalNamespaces =
    [
        "Bugget.Controllers",
        "Bugget.Hubs",
        "Bugget.Middlewares",
        "Bugget.Extensions",
        "Bugget.Authentication",
        "Bugget.HostedServices",
        "Bugget.Configurations",
        "Bugget.Logging",
        "Bugget.ExternalSearch",
    ];

    private const string Bo = "Bugget.BO";
    private const string Ports = "Bugget.BO.Ports";
    private const string Da = "Bugget.DA";
    private const string DbUp = "Bugget.DbUp";
    private const string ExternalClients = "Bugget.ExternalClients";

    private static readonly Assembly ApiAsm = typeof(global::Bugget.AssemblyMarker).Assembly;
    private static readonly Assembly BoAsm = typeof(global::Bugget.BO.AssemblyMarker).Assembly;
    private static readonly Assembly DaAsm = typeof(global::Bugget.DA.AssemblyMarker).Assembly;
    private static readonly Assembly DbUpAsm = typeof(global::Bugget.DbUp.AssemblyMarker).Assembly;
    private static readonly Assembly ExternalClientsAsm = typeof(global::Bugget.ExternalClients.AssemblyMarker).Assembly;
    private static readonly Assembly EntitiesAsm = typeof(global::Bugget.Entities.AssemblyMarker).Assembly;
    private static readonly Assembly TaskQueueAsm = typeof(global::TaskQueue.AssemblyMarker).Assembly;

    private static readonly string[] EntitiesAllowedRoots =
    [
        "System",
        "Microsoft.AspNetCore",      // некоторые DTO ссылаются на IFormFile и пр.
        "Microsoft.Extensions",
        "Bugget.Entities",
    ];

    private static readonly string[] TaskQueueAllowedRoots =
    [
        "System",
        "Microsoft.Extensions",
        "TaskQueue",
    ];

    [Fact(DisplayName = "Bugget.Entities — только System / Microsoft / Bugget.Entities")]
    public void Entities_should_only_depend_on_allowlist()
    {
        var result = Types
            .InAssembly(EntitiesAsm)
            .That()
            .ResideInNamespaceStartingWith("Bugget.Entities")
            .Should()
            .OnlyHaveDependenciesOn(EntitiesAllowedRoots)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Bugget.Entities — самый нижний слой и должен зависеть только от {0}. " +
            "Если ему нужен тип из другого слоя — этот тип должен переехать сюда " +
            "или появиться новый базовый проект. " +
            "Failing types: {1}",
            string.Join(", ", EntitiesAllowedRoots),
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact(DisplayName = "Bugget.DA видит из Bugget.BO только порты")]
    public void Da_should_not_depend_on_upper_layers()
    {
        // После инверсии (ADR-0001) ребро Bugget.DA → Bugget.BO законно, но ровно в одну
        // точку: namespace портов. Всё остальное в бизнес-логике — сервисы, доменные
        // события, мапперы — для инфраструктуры по-прежнему верхний слой.
        //
        // Список запрещённых namespace'ов не ведётся руками: он снимается с самой сборки
        // Bugget.BO, поэтому новый namespace в бизнес-логике попадает под правило сам.
        // Отбрасываются префиксы Bugget.BO.Ports — сам "Bugget.BO" тоже префикс портов
        // при startsWith-сравнении NetArchTest, иначе правило запретило бы и их.
        var boNamespaces = BoAsm.GetTypes()
            .Select(type => type.Namespace)
            .Where(ns => ns is not null
                         && ns.StartsWith(Bo, StringComparison.Ordinal)
                         && !Ports.StartsWith(ns, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var disallowed = boNamespaces
            .Concat([ExternalClients])
            .Concat(ApiInternalNamespaces)
            .ToArray();

        var result = Types
            .InAssembly(DaAsm)
            .Should()
            .NotHaveDependencyOnAny(disallowed!)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Bugget.DA нарушил направление зависимостей: инфраструктура реализует порты из " +
            $"{Ports} и больше ничего из бизнес-логики знать не должна — ни сервисов, ни " +
            "доменных событий, ни HTTP-слоя. Нужен новый контракт — объяви его портом в " +
            "Bugget.BO/Ports и реализуй здесь. " +
            "Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact(DisplayName = "Bugget.BO не зависит от Bugget (API) и Bugget.ExternalClients")]
    public void Bo_should_not_depend_on_api_layer()
    {
        var disallowed = new[] { ExternalClients }
            .Concat(ApiInternalNamespaces)
            .ToArray();

        var result = Types
            .InAssembly(BoAsm)
            .Should()
            .NotHaveDependencyOnAny(disallowed)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Bugget.BO нарушил направление зависимостей: бизнес-логика не должна знать " +
            "про HTTP/SignalR/контроллеры. Если BO нужен контракт API — объяви " +
            "интерфейс в Bugget.BO, реализуй в Bugget. " +
            "Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact(DisplayName = "TaskQueue — System / Microsoft.Extensions / TaskQueue only")]
    public void TaskQueue_should_only_depend_on_allowlist()
    {
        var result = Types
            .InAssembly(TaskQueueAsm)
            .That()
            .ResideInNamespaceStartingWith("TaskQueue")
            .Should()
            .OnlyHaveDependenciesOn(TaskQueueAllowedRoots)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "TaskQueue — переиспользуемая абстракция background queue и должен зависеть " +
            "только от {0}. " +
            "Failing types: {1}",
            string.Join(", ", TaskQueueAllowedRoots),
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact(DisplayName = "Controllers (Bugget.Controllers.*) не используют Bugget.DA напрямую")]
    public void Controllers_should_not_use_data_access_directly()
    {
        // Контроллер — тонкий: делегирует в *Service из Bugget.BO. Прямое обращение
        // к DbClient'ам ломает слой бизнес-логики и обходит транзакции/события.
        var result = Types
            .InAssembly(ApiAsm)
            .That()
            .ResideInNamespaceStartingWith("Bugget.Controllers")
            .Should()
            .NotHaveDependencyOn(Da)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Контроллер пошёл в Bugget.DA в обход Bugget.BO. " +
            "Перенеси вызов в *Service в Bugget.BO/Services и вызови сервис из контроллера. " +
            "Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact(DisplayName = "Bugget.DbUp зависит только от Bugget.Entities + BCL/инфраструктуры миграций")]
    public void DbUp_should_only_depend_on_entities_and_bcl()
    {
        // DbUp — отдельный leaf-проект для накатывания SQL-миграций; ему нечего знать
        // ни о бизнес-логике (Bugget.BO), ни о data access слое (Bugget.DA),
        // ни о HTTP-слое (Bugget). Зависит только от BCL/Microsoft.Extensions,
        // библиотеки DbUp и Bugget.Entities (для констант env).
        var disallowed = new[] { Bo, Da, ExternalClients }
            .Concat(ApiInternalNamespaces)
            .ToArray();

        var result = Types
            .InAssembly(DbUpAsm)
            .Should()
            .NotHaveDependencyOnAny(disallowed)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Bugget.DbUp должен оставаться leaf-проектом. " +
            "Любой контракт, который ему нужен от других слоёв, переноси в Bugget.Entities. " +
            "Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact(DisplayName = "Bugget.ExternalClients не зависит от Bugget.DA вообще")]
    public void ExternalClients_should_not_depend_on_data_access()
    {
        // External integration'ы (Kaiten, users-api, mattermost) — такая же инфраструктура,
        // как и Bugget.DA, и разговаривают с системой через порты Bugget.BO.Ports.
        // Раньше правило разрешало им Bugget.DA.Interfaces, потому что порты жили там;
        // после инверсии (ADR-0001) в Bugget.DA не осталось ничего, что им нужно.
        var result = Types
            .InAssembly(ExternalClientsAsm)
            .Should()
            .NotHaveDependencyOn(Da)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Bugget.ExternalClients залез в Bugget.DA. Нужен контракт — объяви порт в " +
            $"{Ports} и инжекти его; реализация остаётся в инфраструктуре. " +
            "Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
