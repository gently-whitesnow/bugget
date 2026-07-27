using System.Globalization;
using System.Net;
using System.Net.Http.Json;
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
    /// рабочее пространство одно и создаётся на старте, поэтому ручка отвечает 403.
    /// Снимок фиксирует именно это: путь живой, но действие закрыто.
    /// </summary>
    [Fact(DisplayName = "POST /v1/workspaces: в self-hosted режиме 403")]
    public async Task CreateWorkspace()
    {
        var userId = await UsersScenario.CreateUserAsync(fixture);
        var client = fixture.CreateAuthorizedClient("0", "0", userId.ToString(CultureInfo.InvariantCulture));

        var response = await client.PostAsJsonAsync("/v1/workspaces", new { name = "новое пространство" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await ContractSnapshot.MatchAsync("users.v1.workspaces.post", response);
    }

    [Fact(DisplayName = "GET /v1/workspaces: 200 и контекст пользователя")]
    public async Task GetWorkspacesContext()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.GetAsync("/v1/workspaces");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("users.v1.workspaces.get", response);
    }

    [Fact(DisplayName = "PUT /v1/workspaces/{workspaceId}: в self-hosted режиме 403")]
    public async Task UpdateWorkspace()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.PutAsJsonAsync(
            $"/v1/workspaces/{scenario.WorkspaceId}",
            new { name = "переименованное" });

        await ContractSnapshot.MatchAsync("users.v1.workspaces.put", response);
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

        await ContractSnapshot.MatchAsync("users.v1.workspace-members-join.post", response);
    }

    [Fact(DisplayName = "POST /v1/workspaces/{workspaceId}/teams: 200 и форма Team")]
    public async Task CreateTeam()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.PostAsJsonAsync(
            $"/v1/workspaces/{scenario.WorkspaceId}/teams",
            new { name = "команда " + Guid.NewGuid().ToString("N")[..8] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("users.v1.teams.post", response);
    }

    [Fact(DisplayName = "PUT /v1/workspaces/{workspaceId}/teams/{teamId}: 200")]
    public async Task UpdateTeam()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.PutAsJsonAsync(
            $"/v1/workspaces/{scenario.WorkspaceId}/teams/{scenario.TeamId}",
            new { name = "переименованная" });

        await ContractSnapshot.MatchAsync("users.v1.teams.put", response);
    }

    [Fact(DisplayName = "POST /v1/workspaces/{workspaceId}/teams/batch/list: 200, массив команд")]
    public async Task ListTeamsBatch()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.PostAsJsonAsync(
            $"/v1/workspaces/{scenario.WorkspaceId}/teams/batch/list",
            new[] { scenario.TeamId.ToString(CultureInfo.InvariantCulture) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("users.v1.teams-batch-list.post", response);
    }

    [Fact(DisplayName = "GET /v1/workspaces/{workspaceId}/teams/autocomplete: 200")]
    public async Task AutocompleteTeams()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.GetAsync(
            $"/v1/workspaces/{scenario.WorkspaceId}/teams/autocomplete?query=team");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("users.v1.teams-autocomplete.get", response);
    }

    [Fact(DisplayName = "GET .../teams/{teamId}/members: 200 и форма TeamMembers")]
    public async Task ListTeamMembers()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.GetAsync(scenario.TeamPath("/members"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("users.v1.team-members.get", response);
    }

    [Fact(DisplayName = "GET .../teams/{teamId}/members: нечисловой workspaceId в пути — ответ тот же, не 400")]
    public async Task ListTeamMembersWithNonNumericWorkspace()
    {
        // Команду ручка берёт из пути, а рабочее пространство — из identity: сегмент
        // workspaceId до contract-first не связывался, и мусор в нём доезжал до
        // действия. Контракт описывает его строкой, чтобы так и осталось.
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.GetAsync($"/v1/workspaces/not-a-number/teams/{scenario.TeamId}/members");

        // Снимок формы здесь не снимается: команда по умолчанию общая на прогон, и
        // состав участников зависит от соседних сценариев. Проверяется ровно то,
        // ради чего тест написан — запрос доезжает до действия.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "POST .../teams/{teamId}/members/join и DELETE .../members: 200")]
    public async Task JoinAndLeaveTeam()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var joined = await scenario.Client.PostAsync(scenario.TeamPath("/members/join"), null);
        await ContractSnapshot.MatchAsync("users.v1.team-members-join.post", joined);

        var left = await scenario.Client.DeleteAsync(scenario.TeamPath("/members"));
        await ContractSnapshot.MatchAsync("users.v1.team-members.delete", left);
    }

    [Fact(DisplayName = "DELETE .../teams/{teamId}/members/{userId}: 200")]
    public async Task RemoveTeamMember()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);
        await scenario.Client.PostAsync(scenario.TeamPath("/members/join"), null);

        var response = await scenario.Client.DeleteAsync(scenario.TeamPath($"/members/{scenario.UserId}"));

        await ContractSnapshot.MatchAsync("users.v1.team-members-by-id.delete", response);
    }

    [Fact(DisplayName = "Инвайты команды: POST, GET, PUT, приём и DELETE")]
    public async Task TeamInviteLifecycle()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var created = await scenario.Client.PostAsync(scenario.TeamPath("/invites"), null);
        await ContractSnapshot.MatchAsync("users.v1.team-invites.post", created);

        var listed = await scenario.Client.GetAsync(scenario.TeamPath("/invites"));
        await ContractSnapshot.MatchAsync("users.v1.team-invites.get", listed);

        // Токен фронт достаёт из ссылки-приглашения: отдельного поля в ответе нет.
        var inviteLink = (await ContractScenario.ReadJsonAsync(created)).GetProperty("invite_link").GetString()!;
        var token = inviteLink[(inviteLink.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..];

        var accepted = await scenario.Client.PostAsJsonAsync("/v1/invites/accept", new { token });
        await ContractSnapshot.MatchAsync("users.v1.invites-accept.post", accepted);

        var inviteId = (await ContractScenario.ReadJsonAsync(listed)).GetProperty("id").GetInt32();
        var regenerated = await scenario.Client.PutAsync(scenario.TeamPath($"/invites/{inviteId}"), null);
        await ContractSnapshot.MatchAsync("users.v1.team-invites.put", regenerated);

        var deleted = await scenario.Client.DeleteAsync(scenario.TeamPath($"/invites/{inviteId}"));
        await ContractSnapshot.MatchAsync("users.v1.team-invites.delete", deleted);
    }

    [Fact(DisplayName = "DELETE /v1/workspaces/{workspaceId}: в self-hosted режиме 403")]
    public async Task DeleteWorkspace()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.DeleteAsync($"/v1/workspaces/{scenario.WorkspaceId}");

        await ContractSnapshot.MatchAsync("users.v1.workspaces.delete", response);
    }

    [Fact(DisplayName = "DELETE /v1/workspaces/{workspaceId}/teams/{teamId}: 200")]
    public async Task DeleteTeam()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.DeleteAsync(
            $"/v1/workspaces/{scenario.WorkspaceId}/teams/{scenario.TeamId}");

        await ContractSnapshot.MatchAsync("users.v1.teams.delete", response);
    }
}
