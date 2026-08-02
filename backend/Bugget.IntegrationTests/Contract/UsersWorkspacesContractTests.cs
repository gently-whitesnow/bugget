using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контракт модуля users в части рабочих пространств, команд, участников и приглашений.
/// Фронт ходит по этим путям через <c>/api/users/v1/*</c>: nginx срезает префикс,
/// поэтому здесь пути без него.
/// </summary>
[Collection("PostgresCollection")]
public sealed class UsersWorkspacesContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    /// <summary>
    /// В self-hosted сборке (SelfHostedOptions.Enabled = true в appsettings.json)
    /// рабочее пространство одно и создаётся на старте, поэтому ручка отвечает 403
    /// с кодом <c>self_hosted_mode_error</c>: путь живой, но действие закрыто.
    /// </summary>
    [Fact(DisplayName = "POST /v1/workspaces: в self-hosted режиме 403")]
    public async Task CreateWorkspace()
    {
        var userId = await UsersScenario.CreateUserAsync(fixture);
        var client = fixture.CreateAuthorizedClient("0", "0", userId.ToString(CultureInfo.InvariantCulture));

        var response = await client.PostAsJsonAsync("/v1/workspaces", new { name = "новое пространство" });

        await ContractResponse.ProblemAsync(response, "self_hosted_mode_error", HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "GET /v1/workspaces: 200 и контекст пользователя")]
    public async Task GetWorkspacesContext()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.GetAsync("/v1/workspaces");

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);

        // В контексте идентификаторы приходят строками (в соседних ручках модуля
        // встречаются и числа) — фронт разбирает каждую ручку по её схеме, поэтому
        // тип фиксируется поимённо.
        var workspace = Assert.Single(
            body.GetProperty("workspaces").EnumerateArray().ToArray(),
            item => item.GetProperty("id").GetString()
                == scenario.WorkspaceId.ToString(CultureInfo.InvariantCulture));
        Assert.Contains(
            workspace.GetProperty("teams").EnumerateArray(),
            team => team.GetProperty("id").GetString() == scenario.TeamId.ToString(CultureInfo.InvariantCulture));

        Assert.Contains(
            body.GetProperty("workspaces_member").EnumerateArray(),
            member => member.GetProperty("user_id").GetString()
                == scenario.UserId.ToString(CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName = "PUT /v1/workspaces/{workspaceId}: в self-hosted режиме 403")]
    public async Task UpdateWorkspace()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.PutAsJsonAsync(
            $"/v1/workspaces/{scenario.WorkspaceId}",
            new { name = "переименованное" });

        await ContractResponse.ProblemAsync(response, "self_hosted_mode_error", HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST /v1/workspaces/{workspaceId}/members/join: 200 и форма WorkspaceMember")]
    public async Task JoinWorkspace()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);
        var otherUser = await UsersScenario.CreateUserAsync(fixture);
        var client = fixture.CreateAuthorizedClient(
            scenario.WorkspaceId.ToString(CultureInfo.InvariantCulture),
            "0",
            otherUser.ToString(CultureInfo.InvariantCulture));

        var response = await client.PostAsync($"/v1/workspaces/{scenario.WorkspaceId}/members/join", null);

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        // `user_id` — канонический Int64 строкой (shared.yaml `Int64String`).
        Assert.Equal(
            otherUser.ToString(CultureInfo.InvariantCulture),
            body.GetProperty("user_id").GetString());
        Assert.Equal(scenario.WorkspaceId, body.GetProperty("workspace_id").GetInt32());

        // Вступивший вторым — уже не владелец: роль назначает сервер, а не клиент.
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("role").GetString()));
    }

    [Fact(DisplayName = "POST /v1/workspaces/{workspaceId}/teams: 200 и форма Team")]
    public async Task CreateTeam()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.PostAsJsonAsync(
            $"/v1/workspaces/{scenario.WorkspaceId}/teams",
            new { name = "команда " + Guid.NewGuid().ToString("N")[..8] });

        // Создание команды отдаёт идентификаторы числами — в отличие от строковых
        // в контексте рабочих пространств и в batch/list.
        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.True(body.GetProperty("id").GetInt32() > 0);
        Assert.Equal(scenario.WorkspaceId, body.GetProperty("workspace_id").GetInt32());
    }

    [Fact(DisplayName = "PUT /v1/workspaces/{workspaceId}/teams/{teamId}: 200")]
    public async Task UpdateTeam()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.PutAsJsonAsync(
            $"/v1/workspaces/{scenario.WorkspaceId}/teams/{scenario.TeamId}",
            new { name = "переименованная" });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(scenario.TeamId, body.GetProperty("id").GetInt32());
        Assert.Equal("переименованная", body.GetProperty("name").GetString());
    }

    [Fact(DisplayName = "POST /v1/workspaces/{workspaceId}/teams/batch/list: 200, массив команд")]
    public async Task ListTeamsBatch()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.PostAsJsonAsync(
            $"/v1/workspaces/{scenario.WorkspaceId}/teams/batch/list",
            new[] { scenario.TeamId.ToString(CultureInfo.InvariantCulture) });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        var team = Assert.Single(body.EnumerateArray().ToArray());
        Assert.Equal(scenario.TeamId.ToString(CultureInfo.InvariantCulture), team.GetProperty("id").GetString());
    }

    [Fact(DisplayName = "GET /v1/workspaces/{workspaceId}/teams/autocomplete: 200")]
    public async Task AutocompleteTeams()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.GetAsync(
            $"/v1/workspaces/{scenario.WorkspaceId}/teams/autocomplete?query=team");

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        var teams = body.GetProperty("teams").EnumerateArray().ToArray();
        Assert.True(body.GetProperty("total").GetInt32() >= teams.Length);
    }

    [Fact(DisplayName = "GET .../teams/{teamId}/members: 200 и форма TeamMembers")]
    public async Task ListTeamMembers()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.GetAsync(scenario.TeamPath("/members"));

        // size_limit фронт использует как границу для приглашений и получает его
        // вместе со списком, а не отдельной ручкой. В self-hosted сборке лимита нет,
        // и на провод уходит 0 (TeamMembersController).
        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(0, body.GetProperty("size_limit").GetInt32());
        Assert.Equal(JsonValueKind.Array, body.GetProperty("members").ValueKind);
    }

    [Fact(DisplayName = "GET .../teams/{teamId}/members: нечисловой workspaceId в пути — ответ тот же, не 400")]
    public async Task ListTeamMembersWithNonNumericWorkspace()
    {
        // Команду ручка берёт из пути, а рабочее пространство — из identity: сегмент
        // workspaceId до contract-first не связывался, и мусор в нём доезжал до
        // действия. Контракт описывает его строкой, чтобы так и осталось.
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.GetAsync($"/v1/workspaces/not-a-number/teams/{scenario.TeamId}/members");

        // Состав участников здесь не проверяется: команда по умолчанию общая на
        // прогон и зависит от соседних сценариев. Проверяется ровно то, ради чего
        // тест написан, — запрос доезжает до действия.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "POST .../teams/{teamId}/members/join и DELETE .../members: 200")]
    public async Task JoinAndLeaveTeam()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var joined = await scenario.Client.PostAsync(scenario.TeamPath("/members/join"), null);
        await ContractResponse.EmptyAsync(joined, HttpStatusCode.OK);

        var left = await scenario.Client.DeleteAsync(scenario.TeamPath("/members"));
        await ContractResponse.EmptyAsync(left, HttpStatusCode.OK);
    }

    [Fact(DisplayName = "DELETE .../teams/{teamId}/members/{userId}: 200")]
    public async Task RemoveTeamMember()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);
        await scenario.Client.PostAsync(scenario.TeamPath("/members/join"), null);

        var response = await scenario.Client.DeleteAsync(scenario.TeamPath($"/members/{scenario.UserId}"));

        await ContractResponse.EmptyAsync(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName = "DELETE /v1/workspaces/{workspaceId}: в self-hosted режиме 403")]
    public async Task DeleteWorkspace()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.DeleteAsync($"/v1/workspaces/{scenario.WorkspaceId}");

        await ContractResponse.ProblemAsync(response, "self_hosted_mode_error", HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "DELETE /v1/workspaces/{workspaceId}/teams/{teamId}: 200")]
    public async Task DeleteTeam()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.DeleteAsync(
            $"/v1/workspaces/{scenario.WorkspaceId}/teams/{scenario.TeamId}");

        await ContractResponse.EmptyAsync(response, HttpStatusCode.OK);
    }
}
