namespace Bugget.Architecture.Tests;

/// <summary>
/// Известное отступление от целевой архитектуры: зафиксировано явно, а не замолчано.
/// </summary>
/// <param name="From">Проект, который отступает.</param>
/// <param name="To">Проект или сборка, на которую он ссылается вопреки целевому правилу.</param>
/// <param name="What">Что именно нарушено.</param>
/// <param name="Why">Почему это ещё живо и чем снимается.</param>
/// <param name="Adr">ADR, в котором записано целевое состояние.</param>
public sealed record Deviation(string From, string To, string What, string Why, string Adr)
{
    public override string ToString() => $"{From} → {To}: {What} (целевое состояние — {Adr}; {Why})";
}

/// <summary>
/// Реестр отступлений текущего графа проектов от целевой раскладки ADR-0001.
///
/// Правило чтения: это долг, а не разрешение. Пополнять список — значит осознанно
/// увеличивать долг, и такой коммит нужно обосновывать так же, как отключение гейта
/// (ADR-0002). Нормальное движение — только вычёркивание строк.
///
/// Каждая запись проверяется тестом «Известные отступления не протухли»: как только
/// отступление снято в коде, строка обязана исчезнуть отсюда, иначе гейт краснеет.
/// Так список не превращается в кладбище неактуальных исключений.
/// </summary>
public static class KnownDeviations
{
    /// <summary>Рёбра «бизнес-логика → инфраструктура», которые сейчас есть в графе проектов.</summary>
    public static readonly IReadOnlyList<Deviation> BoProjectReferences =
    [
        new("Bugget.BO", "Bugget.DA",
            "бизнес-логика ссылается на data access и транзитивно получает Npgsql и Dapper",
            "порты I*DbClient объявлены внутри Bugget.DA, инверсия зависимости — отдельная задача программы",
            "ADR-0001"),

        new("Users.BO", "Users.DA",
            "то же самое в модуле Users",
            "Users.* растворяется в целевом квартете, чинить точечно дороже, чем при переезде",
            "ADR-0001"),

        new("Users.BO", "Flow",
            "проект Flow собран как Microsoft.NET.Sdk.Web, то есть тащит ASP.NET в бизнес-логику",
            "Flow выпиливается целиком вместе с Monade",
            "ADR-0004"),
    ];

    /// <summary>Сборки, которые бизнес-логика тянет напрямую в обход целевого правила.</summary>
    public static readonly IReadOnlyList<Deviation> BoAssemblyReferences =
    [
        new("Bugget.BO", "System.Data.Common",
            "контракт Bugget.BO.DomainEvents.Consumer.IDomainEventHandler принимает IDbConnection " +
            "и IDbTransaction, то есть ADO.NET виден в сигнатурах бизнес-логики",
            "хендлеры доменных событий работают внутри транзакции poller'а; правильная форма — " +
            "передавать единицу работы через порт, а не через типы System.Data. " +
            "Сам драйвер (Npgsql, Dapper) в Bugget.BO уже не протекает",
            "ADR-0001"),

        new("Users.BO", "Microsoft.Extensions.Http",
            "Users.BO.Avatars.AvatarDownloadService инжектит IHttpClientFactory и сам ходит в сеть",
            "поход наружу должен уехать за порт в Infrastructure, переносится при переезде Users.*",
            "ADR-0001"),

        new("Users.BO", "System.Net.Http",
            "тот же AvatarDownloadService использует HttpClient напрямую",
            "снимается вместе с предыдущей строкой",
            "ADR-0001"),

        new("Users.BO", "System.Net.Primitives",
            "и читает HttpStatusCode ответа",
            "снимается вместе с предыдущей строкой",
            "ADR-0001"),
    ];

    /// <summary>Проекты <c>*.Entities</c>, которые сейчас не являются листьями графа.</summary>
    public static readonly IReadOnlyList<Deviation> EntitiesProjectReferences =
    [
        new("Users.Entities", "Authentication",
            "нижний слой ссылается на проект аутентификации, собранный как Microsoft.NET.Sdk.Web",
            "сам код Users.Entities этой ссылкой не пользуется: через неё Authentication доезжает " +
            "транзитивно до Users.BO и Users.Api, которые её действительно используют. Снимается " +
            "прямыми ссылками при переезде Users.*",
            "ADR-0001"),
    ];

    /// <summary>Все отступления одним списком — для сообщений и проверки на протухание.</summary>
    public static IReadOnlyList<Deviation> All =>
    [
        .. BoProjectReferences,
        .. BoAssemblyReferences,
        .. EntitiesProjectReferences,
    ];

    /// <summary>Разрешённые цели отступлений для проекта — то, что правило обязано пропустить.</summary>
    public static IReadOnlyCollection<string> TargetsFor(IReadOnlyList<Deviation> deviations, string project) =>
        [.. deviations.Where(d => d.From == project).Select(d => d.To)];
}
