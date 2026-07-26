using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контракт модуля users в части профиля: сам пользователь, аватар, привязки
/// провайдеров и Mattermost. Фронт зовёт эти пути с контекстом workspace/team
/// (<c>usersPathWithContext</c>), поэтому и здесь они с ним.
/// </summary>
[Collection("PostgresCollection")]
public sealed class UsersProfileContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    [Fact(DisplayName = "GET .../users: 200 и форма User")]
    public async Task GetUser()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.GetAsync(scenario.TeamPath("/users"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("users.v1.users.get", response);
    }

    [Fact(DisplayName = "PUT .../users: 200 и форма User")]
    public async Task PutUser()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.PutAsJsonAsync(
            scenario.TeamPath("/users"),
            new { name = "Новое имя" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("users.v1.users.put", response);
    }

    [Fact(DisplayName = "POST .../users/batch/list: 200, массив пользователей")]
    public async Task ListUsersBatch()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.PostAsJsonAsync(
            scenario.TeamPath("/users/batch/list"),
            new[] { scenario.UserId.ToString(CultureInfo.InvariantCulture) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("users.v1.users-batch-list.post", response);
    }

    [Fact(DisplayName = "GET .../users/autocomplete: 200 и форма AutocompleteUsers")]
    public async Task AutocompleteUsers()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);
        await scenario.Client.PostAsync(scenario.TeamPath("/members/join"), null);

        var response = await scenario.Client.GetAsync(
            scenario.TeamPath("/users/autocomplete?searchString=&skip=0&take=10"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("users.v1.users-autocomplete.get", response);
    }

    [Fact(DisplayName = "Аватар: POST, GET content, GET {userId}/content, DELETE")]
    public async Task AvatarLifecycle()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var uploaded = await scenario.Client.PostAsync(
            scenario.TeamPath("/users/avatar"),
            ContractScenario.FileContent("avatar.png"));
        await ContractSnapshot.MatchAsync("users.v1.users-avatar.post", uploaded);

        var content = await scenario.Client.GetAsync(scenario.TeamPath("/users/avatar/content"));
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);

        var byId = await scenario.Client.GetAsync(scenario.TeamPath($"/users/{scenario.UserId}/avatar/content"));
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);

        var deleted = await scenario.Client.DeleteAsync(scenario.TeamPath("/users/avatar"));
        await ContractSnapshot.MatchAsync("users.v1.users-avatar.delete", deleted);
    }

    [Fact(DisplayName = "GET .../users/external-links: 200, массив привязок")]
    public async Task GetExternalLinks()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.GetAsync(scenario.TeamPath("/users/external-links"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("users.v1.users-external-links.get", response);
    }

    [Fact(DisplayName = "DELETE .../users/external-links/{provider}: последнюю привязку снять нельзя")]
    public async Task UnlinkProvider()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.DeleteAsync(
            scenario.TeamPath("/users/external-links/oidc"));

        await ContractSnapshot.MatchAsync("users.v1.users-external-links.delete", response);
    }

    [Fact(DisplayName = "PUT и DELETE .../users/mattermost: привязка Mattermost")]
    public async Task MattermostLink()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var linked = await scenario.Client.PutAsJsonAsync(
            scenario.TeamPath("/users/mattermost"),
            new { mattermost_user_id = "mm-" + scenario.UserId });
        await ContractSnapshot.MatchAsync("users.v1.users-mattermost.put", linked);

        var unlinked = await scenario.Client.DeleteAsync(scenario.TeamPath("/users/mattermost"));
        await ContractSnapshot.MatchAsync("users.v1.users-mattermost.delete", unlinked);
    }

    [Fact(DisplayName = "POST .../users/merge: слияние с другим пользователем")]
    public async Task MergeUsers()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);
        var sourceUserId = await UsersScenario.CreateUserAsync(fixture);

        var response = await scenario.Client.PostAsJsonAsync(
            scenario.TeamPath("/users/merge"),
            new { source_user_id = sourceUserId.ToString(CultureInfo.InvariantCulture) });

        await ContractSnapshot.MatchAsync("users.v1.users-merge.post", response);
    }

    [Fact(DisplayName = "DELETE .../users: 200")]
    public async Task DeleteUser()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.DeleteAsync(scenario.TeamPath("/users"));

        await ContractSnapshot.MatchAsync("users.v1.users.delete", response);
    }
}
