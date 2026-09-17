using FluentAssertions;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Правила слоёв квартета (ADR-0001) по фактическим ссылкам скомпилированных сборок, а не по .csproj (это делает
/// <see cref="SolutionGraphRulesTests"/>). Списки белые: новая сборка у нижних слоёв — красный гейт, даже транзитивная.
/// </summary>
public class LayerDependencyRulesTests
{
    private static readonly string[] Bcl = ["System", "netstandard"];

    /// <summary>
    /// Abstractions, разрешённые Application поимённо: пакет из <see cref="SolutionGraphRulesTests"/> не разрешает свои
    /// транзитивные сборки — запись появляется только вместе с фактической ссылкой и при сохранении границы ADR-0001.
    /// </summary>
    private static readonly string[] MicrosoftExtensionsAbstractions =
    [
        "Microsoft.Extensions.Hosting.Abstractions",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Options",
    ];

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

    [Fact(DisplayName = "Bugget.Application — только System, разрешённые Microsoft.Extensions abstractions и Domain")]
    public void Application_does_not_depend_on_transport_or_persistence()
    {
        var violations = Quartet.FindDisallowedReferences(
            Quartet.Application,
            Quartet.ReferencesOf(Quartet.ApplicationAsm),
            Bcl,
            [.. MicrosoftExtensionsAbstractions, Quartet.Domain, .. KnownDeviations.TargetsFor(
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

    [Fact(DisplayName = "Allowlist Application пропускает abstractions и краснеет на HTTP и non-abstractions")]
    public void Application_allowlist_is_provably_red_and_green()
    {
        var violations = Quartet.FindDisallowedReferences(
            Quartet.Application,
            [
                "System.Runtime",
                Quartet.Domain,
                .. MicrosoftExtensionsAbstractions,
                "Microsoft.Extensions.Http",
                "Microsoft.Extensions.Logging",
            ],
            Bcl,
            [.. MicrosoftExtensionsAbstractions, Quartet.Domain]);

        violations.Should().Equal(
            "Bugget.Application → Microsoft.Extensions.Http",
            "Bugget.Application → Microsoft.Extensions.Logging");
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
        // Доказательство красноты гейта (ADR-0002): та же функция на синтетике — домен «зависит» от Npgsql и ASP.NET.
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
