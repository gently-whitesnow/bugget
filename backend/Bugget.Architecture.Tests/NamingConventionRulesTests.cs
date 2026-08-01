using System.Reflection;
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
    private static readonly string[] PostgresBaseTypeNames =
    [
        "Bugget.Infrastructure.Postgres.PostgresClient",
    ];

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
        var violations = FindDbClientsWithoutApplicationPort(Quartet.InfrastructureAsm.GetTypes());

        violations.Should().BeEmpty(
            "каждый *DbClient обязан реализовывать порт из Bugget.Application/**/Ports. Без порта " +
            "прикладной слой нельзя протестировать без БД. Заведи интерфейс рядом с вызывающим " +
            "кодом и подключи через ': IFooDbClient'. Нарушители: {0}",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "Все Postgres-адаптеры следуют соглашению *DbClient")]
    public void Postgres_adapters_follow_DbClient_naming()
    {
        // Предыдущее правило начинается с имени *DbClient. Эта обратная проверка гарантирует,
        // что конкретный наследник общей Postgres-базы не сможет обойти правило
        // и порт, назвавшись просто *Client или *Repository. NpgsqlTransactionScope и
        // NpgsqlUnitOfWork реализуют отдельные транзакционные порты и в этот naming-rule не входят.
        var postgresBaseTypes = ResolveRequiredTypes(Quartet.InfrastructureAsm, PostgresBaseTypeNames);
        var violations = FindPostgresAdaptersWithoutDbClientSuffix(
            Quartet.InfrastructureAsm.GetTypes(),
            postgresBaseTypes);

        violations.Should().BeEmpty(
            "каждый конкретный наследник PostgresClient — persistence-адаптер и обязан называться " +
            "*DbClient, чтобы попасть под проверку порта. Нарушители: {0}",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "Правило *DbClient краснеет на интерфейсе вне *.Ports")]
    public void DbClient_port_rule_is_provably_red_for_an_interface_outside_ports()
    {
        FindDbClientsWithoutApplicationPort([typeof(LegacyDbClient)])
            .Should().ContainSingle()
            .Which.Should().EndWith(nameof(LegacyDbClient));
    }

    [Fact(DisplayName = "Правило Postgres-адаптеров краснеет на суффиксе вне *DbClient")]
    public void Postgres_adapter_rule_is_provably_red_for_an_unchecked_suffix()
    {
        FindPostgresAdaptersWithoutDbClientSuffix(
                [typeof(EscapingPersistenceClient)],
                [typeof(FixturePostgresClient)])
            .Should().ContainSingle()
            .Which.Should().EndWith(nameof(EscapingPersistenceClient));
    }

    [Fact(DisplayName = "Правило Postgres-адаптеров краснеет, если обязательная база исчезла")]
    public void Postgres_adapter_rule_is_provably_red_for_a_missing_base_type()
    {
        Action act = () => ResolveRequiredTypes(
            Quartet.InfrastructureAsm,
            ["Bugget.Infrastructure.Missing.PostgresClient"]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Bugget.Infrastructure.Missing.PostgresClient*");
    }

    [Fact(DisplayName = "Вызываемая внешняя зависимость Application объявлена портом в **/Ports")]
    public void Callable_application_contracts_reside_in_ports()
    {
        // Признак порта — не суффикс имени, а направление реализации плюс наличие операции:
        // интерфейс прикладного слоя, который реализует конкретный тип инфраструктуры или
        // транспорта и у которого есть хотя бы один обычный метод, — это вызываемая внешняя
        // зависимость (ADR-0001). Интерфейс, описывающий только форму данных (одни
        // property-getters, как IExternalSearchItem), портом не является и в **/Ports не едет.
        var violations = FindCallableContractsOutsidePorts(
            [.. Quartet.InfrastructureAsm.GetTypes(), .. Quartet.ApiAsm.GetTypes()],
            Quartet.ApplicationAsm);

        violations.Should().BeEmpty(
            "интерфейс прикладного слоя с вызываемой операцией, который реализует адаптер " +
            "инфраструктуры или транспорта, обязан жить в Bugget.Application/**/Ports: именно " +
            "туда смотрит направление зависимости. Перенеси контракт в *.Ports рядом с " +
            "вызывающим кодом. Нарушители: {0}",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "Портовый гейт краснеет на вызываемом контракте вне *.Ports")]
    public void Callable_contract_rule_is_provably_red_for_a_method_contract()
    {
        FindCallableContractsOutsidePorts(
                [typeof(FixtureTransportAdapter)],
                typeof(NamingConventionRulesTests).Assembly)
            .Should().ContainSingle()
            .Which.Should().EndWith(nameof(IFixtureCallableContract));
    }

    [Fact(DisplayName = "Портовый гейт разрешает model/result-интерфейс вне *.Ports")]
    public void Callable_contract_rule_allows_a_property_only_contract()
    {
        FindCallableContractsOutsidePorts(
                [typeof(FixtureResultItem)],
                typeof(NamingConventionRulesTests).Assembly)
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "Портовый гейт не трогает продуктовую пару KaitenSearchItem → IExternalSearchItem")]
    public void Callable_contract_rule_allows_the_external_search_item_pair()
    {
        FindCallableContractsOutsidePorts(
                [typeof(global::Bugget.Infrastructure.ExternalClients.Kaiten.Models.KaitenSearchItem)],
                Quartet.ApplicationAsm)
            .Should().BeEmpty();
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

    private static string[] FindDbClientsWithoutApplicationPort(IEnumerable<Type> types) =>
    [
        .. types
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.Name.EndsWith("DbClient", StringComparison.Ordinal))
            .Where(type => !type.GetInterfaces().Any(IsApplicationPort))
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
    ];

    private static string[] FindPostgresAdaptersWithoutDbClientSuffix(
        IEnumerable<Type> types,
        IEnumerable<Type> postgresBaseTypes)
    {
        var bases = postgresBaseTypes.ToArray();

        return
        [
            .. types
                .Where(type => type.IsClass && !type.IsAbstract)
                .Where(type => bases.Any(baseType => baseType.IsAssignableFrom(type)))
                .Where(type => !type.Name.EndsWith("DbClient", StringComparison.Ordinal))
                .Select(type => type.FullName ?? type.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    /// Пары «адаптер → контракт», где адаптер реализует вызываемый интерфейс прикладного слоя,
    /// объявленный вне <c>**/Ports</c>. Отдельная функция, а не тело теста: тем же алгоритмом
    /// прогоняются красная и разрешённая фикстуры.
    /// </summary>
    private static string[] FindCallableContractsOutsidePorts(
        IEnumerable<Type> adapters,
        Assembly contractAssembly) =>
    [
        .. adapters
            .Where(type => type.IsClass && !type.IsAbstract)
            .SelectMany(adapter => adapter.GetInterfaces()
                .Where(contract => contract.Assembly == contractAssembly)
                .Where(IsCallableContract)
                .Where(contract => !IsApplicationPort(contract))
                .Select(contract => $"{adapter.FullName ?? adapter.Name} → {contract.FullName ?? contract.Name}"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
    ];

    /// <summary>
    /// Вызываемый контракт — тот, у которого есть хотя бы одна собственная операция.
    /// Аксессоры свойств и событий (<c>IsSpecialName</c>) операциями не считаются: интерфейс
    /// только со свойствами описывает форму данных, а не зависимость.
    /// </summary>
    private static bool IsCallableContract(Type contract) =>
        contract.GetMethods().Any(method => !method.IsSpecialName);

    private static Type[] ResolveRequiredTypes(Assembly assembly, IEnumerable<string> typeNames) =>
    [
        .. typeNames.Select(typeName => assembly.GetType(typeName, throwOnError: false)
            ?? throw new InvalidOperationException(
                $"Обязательная Postgres-база {typeName} не найдена в {assembly.GetName().Name}"))
    ];

    private interface ILegacyContract;

    private sealed class LegacyDbClient : ILegacyContract;

    private abstract class FixturePostgresClient;

    private sealed class EscapingPersistenceClient : FixturePostgresClient;

    /// <summary>Красная фикстура портового гейта: вызываемый контракт вне <c>*.Ports</c>.</summary>
    private interface IFixtureCallableContract
    {
        Task ExecuteAsync();
    }

    private sealed class FixtureTransportAdapter : IFixtureCallableContract
    {
        public Task ExecuteAsync() => Task.CompletedTask;
    }

    /// <summary>Разрешённая фикстура: интерфейс результата — только форма данных.</summary>
    private interface IFixtureResultItem
    {
        string Id { get; }
    }

    private sealed class FixtureResultItem : IFixtureResultItem
    {
        public required string Id { get; init; }
    }
}
