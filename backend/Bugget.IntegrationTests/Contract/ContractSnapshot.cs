using System;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Снимок публичного контракта одного вызова: запрос (метод и путь), статус, media type
/// и форма тела (<see cref="JsonShape"/>). Снимки лежат текстом в
/// <c>Contract/Snapshots</c> — изменение контракта видно прямо в дифффе PR.
/// </summary>
/// <remarks>
/// Снимок падает и на смене статуса, и на переименовании поля. Перезаписать все
/// снимки после осознанной правки контракта:
/// <c>UPDATE_CONTRACT_SNAPSHOTS=1 dotnet test backend/Bugget.IntegrationTests</c>.
/// Отсутствующий снимок создаётся, но тест при этом падает: молча зелёный первый
/// прогон означал бы, что контракт никто не посмотрел.
/// <para>
/// Строка <c>request:</c> — не украшение: по ней гейт <c>backend-contract-snapshots</c>
/// (<c>scripts/quality/contract-snapshots.py</c>) находит операцию в
/// <c>specs/contracts/**/openapi.yaml</c> и сверяет форму снимка со схемой ответа.
/// Без неё соответствие снимка контракту приходилось бы угадывать по имени файла.
/// </para>
/// </remarks>
internal static partial class ContractSnapshot
{
    private const string UpdateEnvironmentVariable = "UPDATE_CONTRACT_SNAPSHOTS";

    private static readonly string SnapshotsDirectory =
        Path.Combine(Path.GetDirectoryName(SourceFilePath())!, "Snapshots");

    /// <summary>
    /// Сверяет ответ со снимком <paramref name="name"/>.
    /// </summary>
    public static async Task MatchAsync(string name, HttpResponseMessage response)
    {
        var actual = await DescribeAsync(response);
        Match(name, actual);
    }

    /// <summary>
    /// Сверяет произвольный текстовый снимок (например, инвентарь путей).
    /// </summary>
    public static void Match(string name, string actual)
    {
        var path = Path.Combine(SnapshotsDirectory, name + ".txt");
        var normalized = actual.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";

        if (IsUpdateMode())
        {
            Write(path, normalized);
            return;
        }

        if (!File.Exists(path))
        {
            Write(path, normalized);
            Assert.Fail(
                $"Снимок контракта {name} отсутствовал и был создан: {path}. " +
                "Проверьте его глазами и закоммитьте — тест зелёный только со снимком в репозитории.");
        }

        var expected = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
        Assert.Equal(expected, normalized);
    }

    /// <summary>
    /// Сверяет сгенерированный документ в репозитории (путь от корня) с ожидаемым
    /// содержимым. Тот же режим обновления, что и у снимков ответов.
    /// </summary>
    public static void MatchDocument(string repositoryRelativePath, string actual)
    {
        // Contract → Bugget.IntegrationTests → backend → корень репозитория.
        var repositoryRoot = Path.GetFullPath(Path.Combine(SnapshotsDirectory, "..", "..", "..", ".."));
        var path = Path.Combine(repositoryRoot, repositoryRelativePath);
        var normalized = actual.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";

        if (IsUpdateMode() || !File.Exists(path))
        {
            Write(path, normalized);
            if (!IsUpdateMode())
            {
                Assert.Fail($"документ {repositoryRelativePath} отсутствовал и был создан — проверьте и закоммитьте");
            }

            return;
        }

        var expected = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
        Assert.Equal(expected, normalized);
    }

    private static async Task<string> DescribeAsync(HttpResponseMessage response)
    {
        var lines = new List<string>
        {
            $"request: {DescribeRequest(response)}",
            $"status: {(int)response.StatusCode}",
            $"content-type: {response.Content.Headers.ContentType?.MediaType ?? "-"}",
        };

        var body = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(body) && IsJson(response))
        {
            using var document = JsonDocument.Parse(body);
            lines.Add("body:");
            lines.Add(JsonShape.Describe(document.RootElement));
        }
        else
        {
            lines.Add($"body: {(string.IsNullOrEmpty(body) ? "empty" : "non-json")}");
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Метод и шаблон маршрута — то, по чему снимок сопоставляется с операцией контракта.
    /// Query-строка не пишется: схему ответа она не выбирает, а сценарии с разными
    /// параметрами и без того разведены по именам снимков.
    /// </summary>
    /// <remarks>
    /// Шаблон приходит заголовком <see cref="ContractHeaders.MatchedRoute"/> из таблицы
    /// маршрутов живого хоста. Если запрос ни во что не смаршрутизировался (404 на
    /// несуществующем пути), пишется сам путь — угадывать шаблон нечем и незачем.
    /// </remarks>
    private static string DescribeRequest(HttpResponseMessage response)
    {
        var request = response.RequestMessage;
        if (request?.RequestUri is null)
        {
            // HttpClient проставляет RequestMessage на каждый ответ; пустое значение
            // означало бы снимок, собранный не из настоящего вызова.
            throw new InvalidOperationException(
                "у ответа нет запроса — снимок контракта снимается только с настоящего вызова");
        }

        var path = response.Headers.TryGetValues(ContractHeaders.MatchedRoute, out var routes)
            ? StripRouteConstraints(routes.First())
            : request.RequestUri.IsAbsoluteUri
                ? request.RequestUri.AbsolutePath
                : request.RequestUri.OriginalString.Split('?')[0];

        return $"{request.Method.Method} /{path.TrimStart('/')}";
    }

    /// <summary>
    /// Убирает из шаблона ограничения маршрута: <c>{legacyId:int}</c> → <c>{legacyId}</c>.
    /// В OpenAPI ограничений нет, а сопоставление снимка с контрактом должно быть
    /// сравнением строк, а не разбором синтаксиса ASP.NET.
    /// </summary>
    private static string StripRouteConstraints(string pattern) =>
        RouteConstraint().Replace(pattern, "{$1}");

    [GeneratedRegex(@"\{([A-Za-z_][A-Za-z0-9_]*)[^}]*\}")]
    private static partial Regex RouteConstraint();

    private static bool IsJson(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        return mediaType is not null
               && mediaType.Contains("json", StringComparison.OrdinalIgnoreCase);
    }

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static bool IsUpdateMode() =>
        Environment.GetEnvironmentVariable(UpdateEnvironmentVariable) is "1" or "true";

    private static string SourceFilePath([CallerFilePath] string path = "") => path;
}
