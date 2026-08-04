using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Гейт на сам инвентарь: он обязан совпадать с таблицей маршрутов приложения. Иначе
/// новый эндпоинт появляется без решения о покрытии.
/// </summary>
[Collection("PostgresCollection")]
public sealed class PublicContractInventoryTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
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
}
