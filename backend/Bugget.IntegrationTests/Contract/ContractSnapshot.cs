using System;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Снимок публичного контракта одного вызова: статус, media type и форма тела
/// (<see cref="JsonShape"/>). Снимки лежат текстом в <c>Contract/Snapshots</c> —
/// изменение контракта видно прямо в дифффе PR.
/// </summary>
/// <remarks>
/// Снимок падает и на смене статуса, и на переименовании поля. Перезаписать все
/// снимки после осознанной правки контракта:
/// <c>UPDATE_CONTRACT_SNAPSHOTS=1 dotnet test backend/Bugget.IntegrationTests</c>.
/// Отсутствующий снимок создаётся, но тест при этом падает: молча зелёный первый
/// прогон означал бы, что контракт никто не посмотрел.
/// </remarks>
internal static class ContractSnapshot
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
