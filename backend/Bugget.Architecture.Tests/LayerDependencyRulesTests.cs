using FluentAssertions;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Целевые правила слоёв квартета (ADR-0001) на уровне скомпилированных сборок.
///
/// <code>
/// Bugget.Api ──────────► Bugget.Application ──────► Bugget.Domain
///     │                        ▲
///     └──► Bugget.Infrastructure┘      Bugget.Contracts (без зависимостей)
///          (только DI-композиция)
/// </code>
///
/// Проверяется не то, что объявлено в .csproj (это делает <see cref="SolutionGraphRulesTests"/>),
/// а то, на какие сборки код слоя ссылается по факту: транзитивная зависимость в .csproj
/// не видна, а в ссылках сборки видна ровно тогда, когда ею начали пользоваться.
///
/// Списки белые: перечислено разрешённое. Новая сборка в зависимостях нижних слоёв — красный
/// гейт, даже если пакет протащили транзитивно и в .csproj он не появился.
/// </summary>
public class LayerDependencyRulesTests
{
    /// <summary>Части BCL, разрешённые любому слою.</summary>
    private static readonly string[] Bcl = ["System", "netstandard"];

    /// <summary>DI, конфигурация, логирование и хостинг — то, чем прикладной слой пользуется как абстракцией.</summary>
    private static readonly string[] MicrosoftExtensions = ["Microsoft.Extensions"];

    [Fact(DisplayName = "Bugget.Domain не зависит ни от чего, кроме System")]
    public void Domain_depends_on_bcl_only()
    {
        var violations = Quartet.FindDisallowedReferences(
            Quartet.Domain,
            Quartet.ReferencesOf(Quartet.DomainAsm),
            Bcl);

        violations.Should().BeEmpty(
            "домен — лист графа: ни проектов решения, ни пакетов, только BCL. Лишнее: {0}. " +
            "Если доменному типу понадобился транспорт, персистенс или сторонняя библиотека — " +
            "это признак того, что тип доменным не является: его место в Application или Infrastructure.",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "Bugget.Contracts не зависит ни от чего, кроме System")]
    public void Contracts_depend_on_bcl_only()
    {
        var violations = Quartet.FindDisallowedReferences(
            Quartet.Contracts,
            Quartet.ReferencesOf(Quartet.ContractsAsm),
            Bcl);

        violations.Should().BeEmpty(
            "контракты — описание провода наружу и такой же лист графа, как домен: только то, " +
            "что сгенерировано из specs/contracts/**/openapi.yaml (ADR-0005). Лишнее: {0}. " +
            "Команды и результаты прикладного слоя живут в Bugget.Application и превращаются " +
            "в контрактные типы мапперами на границе Bugget.Api.",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "Bugget.Application — только System, Microsoft.Extensions.* и Domain")]
    public void Application_does_not_depend_on_transport_or_persistence()
    {
        var violations = Quartet.FindDisallowedReferences(
            Quartet.Application,
            Quartet.ReferencesOf(Quartet.ApplicationAsm),
            [.. Bcl, .. MicrosoftExtensions, Quartet.Domain],
            [.. KnownDeviations.TargetsFor(
                KnownDeviations.ApplicationAssemblyReferences, Quartet.Application)]);

        violations.Should().BeEmpty(
            "прикладной слой начал пользоваться сборкой вне белого списка: {0}. " +
            "Если это транспорт (Microsoft.AspNetCore.*), HTTP-клиент (Microsoft.Extensions.Http, " +
            "System.Net.Http), драйвер БД (Npgsql, Dapper), обработка медиа (SixLabors.ImageSharp, " +
            "Xabe.FFmpeg, Mime) или сгенерированный контракт (Bugget.Contracts) — правило " +
            "сработало по назначению: объяви порт в Bugget.Application/**/Ports и оставь " +
            "реализацию в Bugget.Infrastructure, а перевод в контракт — маппером в Bugget.Api. " +
            "Текущие отступления — KnownDeviations.ApplicationAssemblyReferences.",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "Bugget.Infrastructure не знает про Bugget.Api")]
    public void Infrastructure_does_not_depend_on_api()
    {
        Quartet.ReferencesOf(Quartet.InfrastructureAsm)
            .Should().NotContain(Quartet.Api,
                "инфраструктура реализует порты прикладного слоя и про транспорт знать не должна. " +
                "Нужен контракт от Api — объяви порт в Bugget.Application/Ports.");
    }

    [Fact(DisplayName = "Правило слоя доказуемо краснеет на подсунутой ссылке")]
    public void Layer_rule_is_provably_red()
    {
        // Гейт без доказательства красноты — это гейт, про который никто не знает, работает ли
        // он (ADR-0002). Прогоняем ту же функцию, что и правила выше, на синтетическом наборе
        // ссылок: домен, который «начал» зависеть от Npgsql и ASP.NET.
        var violations = Quartet.FindDisallowedReferences(
            Quartet.Domain,
            ["System.Runtime", "Npgsql", "Microsoft.AspNetCore.Http.Abstractions"],
            Bcl);

        violations.Should().Equal(
            "Bugget.Domain → Microsoft.AspNetCore.Http.Abstractions",
            "Bugget.Domain → Npgsql");
    }

    [Fact(DisplayName = "Известные отступления в ссылках сборок не протухли")]
    public void Known_assembly_deviations_are_still_real()
    {
        var references = Quartet.ReferencesOf(Quartet.ApplicationAsm);

        var stale = KnownDeviations.ApplicationAssemblyReferences
            .Where(deviation => !references.Contains(deviation.To, StringComparer.Ordinal))
            .Select(deviation => deviation.ToString())
            .ToArray();

        stale.Should().BeEmpty(
            "отступление снято в коде, но осталось в списке KnownDeviations — вычеркни строку. " +
            "Протухло: {0}",
            string.Join("; ", stale));
    }
}
