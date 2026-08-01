using Bugget.Api.Extensions;
using Bugget.Application.Services.ReportLinks;
using Bugget.Infrastructure.ExternalClients.Kaiten;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Шов между Kaiten-адаптером и внутренней записью ссылки отчёта.
///
/// Широкий <see cref="IReportLinksService"/> — HTTP-сценарий контроллера (create/update/delete
/// по alias). Инфраструктурному адаптеру из него нужна ровно одна операция, поэтому он видит
/// узкий <see cref="IReportLinkCreator"/>. Реализация при этом одна: два контракта не должны
/// превращаться в два singleton'а с независимым состоянием.
/// </summary>
public class ReportLinksSeamRulesTests
{
    [Fact(DisplayName = "Bugget.Infrastructure не зависит от широкого IReportLinksService")]
    public void Infrastructure_does_not_depend_on_the_wide_report_links_contract()
    {
        var violations = FindConstructorDependenciesOn(
            Quartet.InfrastructureAsm.GetTypes(),
            typeof(IReportLinksService));

        violations.Should().BeEmpty(
            "адаптеру инфраструктуры нужна одна внутренняя операция записи ссылки, а не весь " +
            "HTTP-сценарий отчётных ссылок: зависимость объявляется узким контрактом " +
            "Bugget.Application.Services.ReportLinks.IReportLinkCreator. Нарушители: {0}",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "KaitenApplyService видит узкий IReportLinkCreator")]
    public void Kaiten_apply_service_sees_the_narrow_contract()
    {
        // Позитивная половина правила: без неё шов «проходит» и после того, как зависимость
        // просто выкинули вместе со сценарием записи ссылки.
        FindConstructorDependenciesOn([typeof(KaitenApplyService)], typeof(IReportLinkCreator))
            .Should().ContainSingle();
    }

    [Fact(DisplayName = "Правило шва краснеет на адаптере с широким контрактом")]
    public void Seam_rule_is_provably_red_for_a_wide_contract_consumer()
    {
        FindConstructorDependenciesOn([typeof(WideContractConsumer)], typeof(IReportLinksService))
            .Should().ContainSingle()
            .Which.Should().StartWith(typeof(WideContractConsumer).FullName);
    }

    [Fact(DisplayName = "Композиция не создаёт второй экземпляр реализации")]
    public void Business_logic_composition_creates_no_duplicate_implementations()
    {
        var duplicates = FindDuplicatedSingletonImplementations(new ServiceCollection().AddBusinessLogic());

        duplicates.Should().BeEmpty(
            "реализация зарегистрирована как singleton больше одного раза — контейнер создаст " +
            "по экземпляру на регистрацию, и потребители контрактов получат разные объекты. " +
            "Регистрируй конкретный тип один раз, а каждый интерфейс отдавай фабрикой " +
            "sp => sp.GetRequiredService<T>(). Дубликаты: {0}",
            string.Join(", ", duplicates));
    }

    [Fact(DisplayName = "Правило дублей краснеет на двух регистрациях одной реализации")]
    public void Duplicate_rule_is_provably_red_for_two_registrations_of_one_implementation()
    {
        var services = new ServiceCollection()
            .AddSingleton<IReportLinksService, ReportLinksService>()
            .AddSingleton<IReportLinkCreator, ReportLinksService>();

        FindDuplicatedSingletonImplementations(services)
            .Should().ContainSingle()
            .Which.Should().Be(typeof(ReportLinksService).FullName);
    }

    /// <summary>
    /// Пары «тип → контракт» для типов, принимающих <paramref name="contract"/> в конструкторе.
    /// Отдельная функция, а не тело теста: ту же проверку прогоняют доказательства красноты.
    /// </summary>
    private static string[] FindConstructorDependenciesOn(IEnumerable<Type> types, Type contract) =>
    [
        .. types
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.GetConstructors()
                .SelectMany(ctor => ctor.GetParameters())
                .Any(parameter => parameter.ParameterType == contract))
            .Select(type => $"{type.FullName ?? type.Name} → {contract.FullName ?? contract.Name}")
            .OrderBy(value => value, StringComparer.Ordinal)
    ];

    /// <summary>
    /// Реализации, которые singleton-регистрации инстанцируют больше одного раза: контейнер
    /// хранит экземпляр на дескриптор, поэтому два дескриптора с одним
    /// <c>ImplementationType</c> — это два объекта. Регистрация интерфейса фабрикой
    /// (<c>ImplementationFactory</c>) собственный экземпляр не создаёт и сюда не попадает.
    /// </summary>
    private static string[] FindDuplicatedSingletonImplementations(IServiceCollection services) =>
    [
        .. services
            .Where(descriptor => descriptor.Lifetime == ServiceLifetime.Singleton)
            .Select(descriptor => descriptor.ImplementationType ?? descriptor.ServiceType)
            .Where(implementation => implementation is { IsClass: true, IsAbstract: false })
            .GroupBy(implementation => implementation.FullName ?? implementation.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
    ];

    /// <summary>Красная фикстура шва: адаптер, тянущий весь HTTP-сценарий ссылок.</summary>
    private sealed class WideContractConsumer(IReportLinksService reportLinksService)
    {
        public IReportLinksService ReportLinksService { get; } = reportLinksService;
    }
}
