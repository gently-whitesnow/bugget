using System.Net;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Границы, где публичный Int64 приходит сегментом адреса. После MAIN-44 сегмент
/// объявлен строкой канона <c>Int64String</c>, и вопрос «а что теперь доезжает до
/// сервиса» перестаёт быть риторическим: строкой в действие проходит что угодно.
///
/// Проверяется, что неканоничный сегмент отбивается на границе и отбивается
/// контрактной ошибкой из общего каталога (ADR-0008), а не 500:
///
///   * ограничение маршрута <c>:long</c> там, где оно было и до contract-first,
///     по-прежнему отвечает 404 на нечисловой и на вылезающий за Int64 сегмент;
///   * всё, что через маршрут проходит, но каноном не является (<c>-5</c>,
///     <c>007</c>), отбивает уже граница действия — тем же 400, каким такой
///     сегмент отбивало связывание модели.
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

    /// <remarks>
    /// Эти два сегмента до действия не доходят вовсе: маршрут с ними не совпадает.
    /// Ответ тот же, что и до перевода поля в строку, — публичное поведение здесь
    /// не менялось.
    /// </remarks>
    [Theory(DisplayName = "GET /v2/reports/{id}/analytics: мусор и выход за Int64 — 404, как и раньше")]
    [InlineData("abc")]
    [InlineData("9223372036854775808")]
    public async Task ReportAnalyticsKeepsRouteMiss(string segment)
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync($"/v2/reports/{segment}/analytics");

        await ContractResponse.ProblemAsync(response, "not_found", HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Канон доезжает до сервиса: репорта с таким идентификатором нет, и ответ —
    /// прикладной 404, а не отказ границы. Значение выбрано за пределом точности
    /// double: именно оно раньше округлялось бы по дороге.
    /// </summary>
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
