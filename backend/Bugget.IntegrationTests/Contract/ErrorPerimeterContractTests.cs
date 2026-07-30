using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Периметр ошибок: ответы, которые формирует не контроллер, а фреймворк, — промах
/// маршрутизации, неподходящий метод, отказ аутентификации и авторизации. До MAIN-69
/// они уходили без тела либо в чужой форме ASP.NET; теперь у каждого есть стабильный
/// <c>code</c> из общего каталога (ADR-0008).
/// </summary>
[Collection("PostgresCollection")]
public sealed class ErrorPerimeterContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    [Fact(DisplayName = "Запрос без identity: 401 с problem+json и кодом unauthorized")]
    public async Task MissingIdentityIsAProblem()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/v2/reports");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProblemAsync(response, "unauthorized", 401);
    }

    /// <summary>
    /// <c>Forbid()</c> — единственный источник 403 в модуле users: identity есть, но
    /// workspace в пути чужой. Тело такого отказа собирает та же граница.
    /// </summary>
    [Fact(DisplayName = "Forbid(): 403 с problem+json и кодом forbidden")]
    public async Task ForbidIsAProblem()
    {
        var scenario = ContractScenario.Create(fixture);
        var foreignWorkspaceId = (int.Parse(scenario.WorkspaceId, CultureInfo.InvariantCulture) + 7)
            .ToString(CultureInfo.InvariantCulture);

        var response = await scenario.Client.GetAsync($"/v1/workspaces/{foreignWorkspaceId}/teams/autocomplete");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, "forbidden", 403);
    }

    [Fact(DisplayName = "Несуществующий маршрут: 404 с problem+json и кодом not_found")]
    public async Task UnknownRouteIsAProblem()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.GetAsync("/v2/reports/counts/nothing-here");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, "not_found", 404);
    }

    [Fact(DisplayName = "Неподходящий метод: 405 с problem+json и кодом method_not_allowed")]
    public async Task WrongMethodIsAProblem()
    {
        var scenario = ContractScenario.Create(fixture);

        var response = await scenario.Client.DeleteAsync("/v2/reports");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        await AssertProblemAsync(response, "method_not_allowed", 405);
    }

    [Fact(DisplayName = "MVC: битый JSON — 400 ValidationProblemDetails с code")]
    public async Task MalformedJsonIsAValidationProblem()
    {
        var scenario = ContractScenario.Create(fixture);
        using var content = new StringContent("{\"title\":", Encoding.UTF8, "application/json");

        var response = await scenario.Client.PostAsync("/v2/reports", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, "model_state_validation_error", 400);
        Assert.True(problem.TryGetProperty("errors", out var errors));
        Assert.True(errors.EnumerateObject().Any());
    }

    [Fact(DisplayName = "MVC: неподдерживаемый Content-Type — 415 problem+json с code")]
    public async Task UnsupportedContentTypeIsAProblem()
    {
        var scenario = ContractScenario.Create(fixture);
        using var content = new StringContent("not-json", Encoding.UTF8, "text/plain");

        var response = await scenario.Client.PostAsync("/v2/reports", content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        await AssertProblemAsync(response, "unsupported_media_type", 415);
    }

    [Fact(DisplayName = "Users: конфликт слияния — 409 problem+json с прикладным code")]
    public async Task UsersConflictIsAProblem()
    {
        var target = await UsersScenario.CreateAsync(fixture);
        var sourceUserId = await UsersScenario.CreateUserAsync(fixture);

        // В self-hosted режиме публичное создание workspace закрыто. Для достижения
        // прикладной conflict-ветки подготавливаем только владение source-пользователя:
        // сам ответ по-прежнему получаем через публичный HTTP endpoint.
        await using (var connection = new NpgsqlConnection(
            Environment.GetEnvironmentVariable("USERS_POSTGRES_CONNECTION_STRING")!))
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO workspaces_members (workspace_id, user_id, role)
                VALUES (@workspaceId, @userId, 'admin')
                ON CONFLICT (workspace_id, user_id) DO UPDATE SET role = 'admin'
                """,
                new { workspaceId = target.WorkspaceId, userId = sourceUserId });
        }

        var response = await target.Client.PostAsJsonAsync(
            target.TeamPath("/users/merge"),
            new { source_user_id = sourceUserId.ToString(CultureInfo.InvariantCulture) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, "source_owns_workspaces", 409);
    }

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, string code, int status)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.Equal($"urn:bugget:error:{code}", root.GetProperty("type").GetString());
        Assert.Equal(status, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        return root.Clone();
    }
}
