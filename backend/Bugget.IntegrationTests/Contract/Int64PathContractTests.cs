using System.Net;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Границы, где публичный Int64 приходит сегментом адреса: после MAIN-44 сегмент — строка <c>Int64String</c>, и в действие
/// проходит что угодно. Неканоничный сегмент должен отбиваться контрактной ошибкой (ADR-0008), а не 500: <c>:long</c>
/// по-прежнему даёт 404 на нечисловой и вылезающий за Int64, а <c>-5</c>/<c>007</c> отбивает граница действия тем же 400.
/// </summary>
[Collection("PostgresCollection")]
public sealed class Int64PathContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    [Theory(DisplayName = "GET /v2/reports/{id}/analytics: неканоничный сегмент — 400, а не 500")]
    [InlineData("-5")]
    [InlineData("007")]
    [InlineData("0009007199254740993")]
    public async Task ReportAnalyticsRejectsNonCanonicalSegment(string segment)
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync($"/v2/reports/{segment}/analytics");

        await ContractResponse.ProblemAsync(
            response,
            "model_state_validation_error",
            HttpStatusCode.BadRequest);
    }

    // Эти сегменты до действия не доходят: маршрут не совпадает, поведение то же, что до перевода поля в строку.
    [Theory(DisplayName = "GET /v2/reports/{id}/analytics: мусор и выход за Int64 — 404, как и раньше")]
    [InlineData("abc")]
    [InlineData("9223372036854775808")]
    public async Task ReportAnalyticsKeepsRouteMiss(string segment)
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync($"/v2/reports/{segment}/analytics");

        await ContractResponse.ProblemAsync(response, "not_found", HttpStatusCode.NotFound);
    }

    // Канон доезжает до сервиса: ответ — прикладной 404, а не отказ границы. Значение за пределом точности double:
    // именно оно раньше округлялось бы по дороге.
    [Fact(DisplayName = "GET /v2/reports/{id}/analytics: канон за 2^53 доходит до сервиса")]
    public async Task ReportAnalyticsAcceptsCanonicalSegment()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync("/v2/reports/9007199254740993/analytics");

        await ContractResponse.ProblemAsync(response, "not_found", HttpStatusCode.NotFound);
    }

    [Theory(DisplayName = "DELETE .../members/{userId}: неканоничный сегмент — 400, а не удаление соседа")]
    [InlineData("-5")]
    [InlineData("007")]
    [InlineData("abc")]
    public async Task DeleteTeamMemberRejectsNonCanonicalSegment(string segment)
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.DeleteAsync(scenario.TeamPath($"/members/{segment}"));

        await ContractResponse.ProblemAsync(
            response,
            "model_state_validation_error",
            HttpStatusCode.BadRequest);
    }

    [Theory(DisplayName = "GET .../users/{userId}/avatar/content: неканоничный сегмент — 400, а не 500")]
    [InlineData("-5")]
    [InlineData("007")]
    public async Task UserAvatarRejectsNonCanonicalSegment(string segment)
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.GetAsync(
            scenario.TeamPath($"/users/{segment}/avatar/content"));

        await ContractResponse.ProblemAsync(
            response,
            "model_state_validation_error",
            HttpStatusCode.BadRequest);
    }
}
