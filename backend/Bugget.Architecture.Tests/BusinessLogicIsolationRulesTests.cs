using System.Reflection;
using FluentAssertions;

namespace Bugget.Architecture.Tests;

/// <summary>
/// Правило уровня скомпилированной сборки: от чего бизнес-логика зависит по факту.
///
/// <see cref="SolutionGraphRulesTests"/> проверяет, что объявлено в .csproj. Здесь
/// проверяется другое: список сборок, на типы которых код *.BO реально ссылается в IL.
/// Разница существенная — транзитивная зависимость (ASP.NET через Flow, Npgsql через DA)
/// в .csproj не видна, а в ссылках сборки видна ровно тогда, когда ею начали пользоваться.
///
/// Список белый: перечислено разрешённое. Новая сборка в зависимостях *.BO — красный гейт,
/// даже если пакет протащили транзитивно и в .csproj он не появился.
/// </summary>
public class BusinessLogicIsolationRulesTests
{
    /// <summary>Части BCL и DI-абстракции, разрешённые любому проекту бизнес-логики.</summary>
    private static readonly string[] Bcl =
    [
        "System.Runtime", "System.Collections", "System.Linq", "System.Console",
        "System.Text.Json", "System.Text.RegularExpressions", "System.Memory",
        "System.Threading", "System.Threading.Channels", "System.ComponentModel.Annotations",
        "netstandard",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Options",
        "Microsoft.Extensions.Configuration.Abstractions",
        "Microsoft.Extensions.Hosting.Abstractions",
    ];

    /// <summary>
    /// Что бизнес-логике разрешено видеть: BCL, DI/логирование, обработка медиа и свои же
    /// проекты. Ни ASP.NET, ни HTTP-клиентов, ни Npgsql/Dapper в списке нет — это и есть
    /// смысл правила.
    /// </summary>
    private static readonly Dictionary<string, string[]> BoAllowedAssemblies = new(StringComparer.Ordinal)
    {
        ["Bugget.BO"] =
        [
            .. Bcl,
            "System.Security.Claims",       // BO сверяет идентичность пользователя из claims
            // Обработка медиа идёт прямо в BO: картинки — ImageSharp, видео — ffmpeg
            // (внешний процесс), архивы вложений — GZip. По ADR-0001 это работа для
            // Infrastructure, но переезд идёт отдельной задачей, а не здесь.
            "SixLabors.ImageSharp", "Xabe.FFmpeg", "Xabe.FFmpeg.Downloader", "Mime",
            "System.Diagnostics.Process", "System.IO.Compression",
            "Bugget.Analytics.Contracts", "Bugget.Entities", "TaskQueue",
        ],

        ["Users.BO"] =
        [
            .. Bcl,
            "System.ComponentModel",
            "System.Security.Cryptography", // HMAC для токенов приглашений в команду
            "Microsoft.Extensions.Configuration.Binder",
            "Bugget.Entities", "TaskQueue", "Users.Entities",
        ],
    };

    private static readonly Dictionary<string, Assembly> BoAssemblies = new(StringComparer.Ordinal)
    {
        ["Bugget.BO"] = typeof(global::Bugget.BO.AssemblyMarker).Assembly,
        ["Users.BO"] = typeof(global::Users.BO.AssemblyMarker).Assembly,
    };

    [Fact(DisplayName = "*.BO ссылается только на разрешённые сборки")]
    public void Bo_assemblies_reference_only_allowlisted_assemblies()
    {
        var violations = new List<string>();

        foreach (var (project, assembly) in BoAssemblies)
        {
            var allowed = BoAllowedAssemblies[project]
                .Concat(KnownDeviations.TargetsFor(KnownDeviations.BoAssemblyReferences, project))
                .ToHashSet(StringComparer.Ordinal);

            violations.AddRange(assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name ?? string.Empty)
                .Where(name => !allowed.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => $"{project} → {name}"));
        }

        violations.Should().BeEmpty(
            "бизнес-логика начала пользоваться сборкой, которой нет в белом списке " +
            "BusinessLogicIsolationRulesTests.BoAllowedAssemblies: {0}. " +
            "Если это транспорт (Microsoft.AspNetCore.*), HTTP-клиент (Microsoft.Extensions.Http, " +
            "System.Net.Http) или драйвер БД (Npgsql, Dapper) — правило сработало по назначению: " +
            "вынеси вызов за интерфейс и оставь реализацию в инфраструктурном проекте. " +
            "Если зависимость по смыслу чистая — добавь её в белый список тем же коммитом, " +
            "чтобы решение было видно в диффе. " +
            "Текущие отступления — KnownDeviations.BoAssemblyReferences.",
            string.Join(", ", violations));
    }

    [Fact(DisplayName = "Известные отступления в ссылках сборок не протухли")]
    public void Known_assembly_deviations_are_still_real()
    {
        var stale = KnownDeviations.BoAssemblyReferences
            .Where(deviation => !BoAssemblies[deviation.From]
                .GetReferencedAssemblies()
                .Any(reference => reference.Name == deviation.To))
            .Select(deviation => deviation.ToString())
            .ToArray();

        stale.Should().BeEmpty(
            "отступление снято в коде, но осталось в списке KnownDeviations — вычеркни строку. " +
            "Протухло: {0}",
            string.Join("; ", stale));
    }
}
