namespace Bugget.Architecture.Tests;

/// <summary>
/// Известное отступление от целевой архитектуры: зафиксировано явно, а не замолчано.
/// </summary>
/// <param name="From">Проект или пространство имён, которое отступает.</param>
/// <param name="To">Проект, сборка или пространство имён, на которое оно ссылается вопреки целевому правилу.</param>
/// <param name="What">Что именно нарушено.</param>
/// <param name="Why">Почему это ещё живо и чем снимается.</param>
/// <param name="Adr">ADR, в котором записано целевое состояние.</param>
public sealed record Deviation(string From, string To, string What, string Why, string Adr)
{
    public override string ToString() => $"{From} → {To}: {What} (целевое состояние — {Adr}; {Why})";
}

/// <summary>
/// Реестр отступлений текущего кода от целевой раскладки ADR-0001. Сейчас пуст: граф
/// проектов совпадает с целевым, а <c>Bugget.Api</c> видит инфраструктуру только из
/// поимённого композиционного корня (<see cref="Quartet.CompositionRoot"/>).
///
/// Правило чтения: это долг, а не разрешение. Пополнять список — значит осознанно
/// увеличивать долг, и такой коммит нужно обосновывать так же, как отключение гейта
/// (ADR-0002). Нормальное движение — только вычёркивание строк.
///
/// Каждая запись проверяется тестом на протухание: как только отступление снято в коде,
/// строка обязана исчезнуть отсюда, иначе гейт краснеет. Так список не превращается
/// в кладбище неактуальных исключений.
/// </summary>
public static class KnownDeviations
{
    /// <summary>Рёбра «прикладной слой → инфраструктура», которые сейчас есть в графе проектов.</summary>
    /// <remarks>
    /// Пусто: <c>Bugget.Application</c> объявляет только <c>Bugget.Domain</c>;
    /// порты живут в <c>Bugget.Application/Ports</c>, а
    /// <c>Bugget.Infrastructure</c> ссылается на прикладной слой, а не наоборот.
    /// </remarks>
    public static readonly IReadOnlyList<Deviation> ApplicationProjectReferences = [];

    /// <summary>Сборки, которые прикладной слой тянет напрямую в обход целевого правила.</summary>
    /// <remarks>
    /// Пусто: поход в сеть за аватаром уехал в <c>Bugget.Infrastructure/Users/Avatars</c>,
    /// а обработка медиа — за порты <c>IAttachmentOptimizer</c> и <c>IMimeTypeDetector</c>
    /// в <c>Bugget.Infrastructure/Attachments</c>. Ни HTTP-клиента, ни ImageSharp, ни ffmpeg,
    /// ни libmagic в прикладном слое не осталось.
    /// </remarks>
    public static readonly IReadOnlyList<Deviation> ApplicationAssemblyReferences = [];

    /// <summary>Все отступления одним списком — для сообщений и проверки на протухание.</summary>
    public static IReadOnlyList<Deviation> All =>
    [
        .. ApplicationProjectReferences,
        .. ApplicationAssemblyReferences,
    ];

    /// <summary>Разрешённые цели отступлений для проекта — то, что правило обязано пропустить.</summary>
    public static IReadOnlyCollection<string> TargetsFor(IReadOnlyList<Deviation> deviations, string project) =>
        [.. deviations.Where(d => d.From == project).Select(d => d.To)];
}
