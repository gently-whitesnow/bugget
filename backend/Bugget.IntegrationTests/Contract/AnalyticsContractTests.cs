using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контракт аналитики. Что попадает в выборку, проверяется в
/// <see cref="AnalyticsControllerTests"/>; здесь — публичный периметр: статус,
/// media type, разбор параметра <c>period</c> и его отражение в ответе.
/// </summary>
[Collection("PostgresCollection")]
public sealed class AnalyticsContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    [Fact(DisplayName = "GET /v2/analytics/summary: 200 и запрошенный период в ответе")]
    public async Task Summary()
    {
        var scenario = ContractScenario.Create(fixture);
        await scenario.CreateReportAsync();

        var response = await scenario.Client.GetAsync("/v2/analytics/summary?period=30d");

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        AssertPeriod(body, "last_30_days");
        Assert.Equal(0, body.GetProperty("reports_closed").GetInt32());
    }

    [Fact(DisplayName = "GET /v2/analytics/responsible/{userId}: 200 и форма AnalyticsResponsible")]
    public async Task Responsible()
    {
        var scenario = ContractScenario.Create(fixture);
        await scenario.CreateReportAsync();

        var response = await scenario.Client.GetAsync(
            $"/v2/analytics/responsible/{scenario.UserId}?period=30d");

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        AssertPeriod(body, "last_30_days");
        Assert.Empty(body.GetProperty("reports_completed").EnumerateArray().ToArray());
    }

    [Fact(DisplayName = "GET /v2/reports/{id}/analytics: 200 и разбивка багов по статусам")]
    public async Task ReportAnalytics()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        var response = await scenario.Client.GetAsync($"/v2/reports/{reportId}/analytics");

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        // `report_id` — канонический Int64 строкой (shared.yaml `Int64String`).
        Assert.Equal(reportId.ToString(CultureInfo.InvariantCulture), body.GetProperty("report_id").GetString());

        // Разбивка приходит всеми четырьмя ключами, даже когда багов нет: фронт
        // рисует по ним фиксированные колонки и на отсутствие ключа не рассчитывает.
        var byStatus = body.GetProperty("bugs_by_status");
        Assert.Equal(
            new[] { "fixed", "open", "rejected", "verified" },
            byStatus.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(byStatus.EnumerateObject(), property => Assert.Equal(0, property.Value.GetInt32()));
    }

    /// <summary>
    /// Невалидный период — единственная ветка аналитики, где 400 собирает контроллер.
    /// Причина берётся из публичного списка допустимых значений: текст исключения —
    /// внутренняя деталь, и вдобавок он отражал бы обратно присланное клиентом значение.
    /// </summary>
    /// <remarks>
    /// Пустой <c>period</c> сюда не входит: он не проходит валидацию модели и до
    /// контроллера не доезжает — это ветка <c>model_state_validation_error</c>.
    /// </remarks>
    [Theory(DisplayName = "Невалидный period: 400 без текста исключения и без эха ввода")]
    [InlineData("/v2/analytics/summary?period=<script>secret</script>")]
    [InlineData("/v2/analytics/responsible/user-1?period=<script>secret</script>")]
    public async Task InvalidPeriodDoesNotEchoTheInput(string path)
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal("invalid_period", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "Допустимые значения: 7d, 30d, 60d, 180d, 360d, all.",
            document.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("secret", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Unknown period value", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Period query parameter is required", body, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "GET /v2/reports/{id}/analytics: нечисловой сегмент не совпадает с маршрутом — 404")]
    public async Task ReportAnalyticsWithNonNumericId()
    {
        // Ограничение маршрута (:long) держит поведение «мусорный сегмент — это не наш
        // путь». Без него запрос доехал бы до действия и вернул 400 на связывании.
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync("/v2/reports/not-a-number/analytics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Период уходит на провод разобранным: в запросе <c>30d</c>, в ответе —
    /// метка каталога (<c>last_30_days</c>) и границы окна. Фронт подписывает
    /// график ответом, а не тем, что отправил.
    /// </summary>
    private static void AssertPeriod(JsonElement body, string label)
    {
        var period = body.GetProperty("period");
        Assert.Equal(label, period.GetProperty("label").GetString());
        Assert.False(string.IsNullOrWhiteSpace(period.GetProperty("from").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(period.GetProperty("to").GetString()));
    }
}
