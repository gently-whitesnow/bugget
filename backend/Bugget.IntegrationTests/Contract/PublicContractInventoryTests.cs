using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Гейт на сам инвентарь: он обязан совпадать с таблицей маршрутов приложения и с
/// документом в <c>docs/</c>. Иначе новый эндпоинт появляется без решения о покрытии,
/// а таблица в документации расходится с кодом.
/// </summary>
[Collection("PostgresCollection")]
public sealed class PublicContractInventoryTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    private const string DocumentPath = "docs/public-contract-inventory.md";

    [Fact(DisplayName = "Инвентарь покрывает все маршруты приложения и не содержит лишних")]
    public void InventoryMatchesRoutes()
    {
        fixture.CreateAnonymousClient();
        var routes = PublicSurface.Routes(fixture.Services);

        var missing = routes.Where(route => !PublicContractInventory.Entries.ContainsKey(route)).ToArray();
        var stale = PublicContractInventory.Entries.Keys
            .Where(route => !routes.Contains(route, StringComparer.Ordinal))
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "новые пути приложения не попали в инвентарь — добавьте строку с решением о покрытии " +
            $"в PublicContractInventory:{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", missing)}");

        Assert.True(
            stale.Length == 0,
            "в инвентаре есть пути, которых больше нет в приложении — удалите строки:" +
            $"{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", stale)}");
    }

    /// <summary>
    /// Документ собирается из инвентаря и потому руками не правится. Пересобрать:
    /// <c>UPDATE_CONTRACT_INVENTORY=1 dotnet test backend/Bugget.IntegrationTests</c>.
    /// </summary>
    [Fact(DisplayName = "Таблица покрытия в docs совпадает с инвентарём")]
    public void InventoryDocumentIsUpToDate()
    {
        // Contract → Bugget.IntegrationTests → backend → корень репозитория.
        var path = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(SourceFilePath())!,
            "..", "..", "..",
            DocumentPath));

        var expected = Render().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";

        if (IsUpdateMode() || !File.Exists(path))
        {
            File.WriteAllText(path, expected);
            Assert.True(
                IsUpdateMode(),
                $"документ {DocumentPath} отсутствовал и был собран — проверьте и закоммитьте");
            return;
        }

        var actual = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
        Assert.True(
            expected == actual,
            $"{DocumentPath} разошёлся с инвентарём — пересоберите: " +
            "UPDATE_CONTRACT_INVENTORY=1 dotnet test backend/Bugget.IntegrationTests");
    }

    private static bool IsUpdateMode() =>
        Environment.GetEnvironmentVariable("UPDATE_CONTRACT_INVENTORY") is "1" or "true";

    private static string SourceFilePath([CallerFilePath] string path = "") => path;

    private static string Render()
    {
        var document = new StringBuilder();
        document.AppendLine("# Инвентарь публичного контракта");
        document.AppendLine();
        document.AppendLine("<!-- Файл собирается тестом PublicContractInventoryTests из таблицы маршрутов");
        document.AppendLine("     приложения и из backend/Bugget.IntegrationTests/Contract/PublicContractInventory.cs.");
        document.AppendLine("     Руками не правится: пересобрать —");
        document.AppendLine("     UPDATE_CONTRACT_INVENTORY=1 dotnet test backend/Bugget.IntegrationTests -->");
        document.AppendLine();
        document.AppendLine("Пути даны так, как их видит бекенд. Фронт ходит по ним через nginx с префиксами");
        document.AppendLine("`/api/app/workspaces/{id}/teams/{id}`, `/api/users` и `/api/authorization`, которые");
        document.AppendLine("срезаются при проксировании (deploy/nginx/snippets/locations).");
        document.AppendLine();
        document.AppendLine("| Путь | Зовёт | Покрыт | Комментарий |");
        document.AppendLine("| --- | --- | --- | --- |");

        foreach (var (route, entry) in PublicContractInventory.Entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var covered = entry.CoveredBy == PublicContractInventory.Uncovered
                ? "нет"
                : $"да — `{entry.CoveredBy}`";

            document.AppendLine($"| `{route}` | {entry.Consumer} | {covered} | {entry.Note} |");
        }

        return document.ToString();
    }
}
