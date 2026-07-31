using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        AssertIsSelf(body, scenario);
    }

    [Fact(DisplayName = "GET .../users: нечисловые workspaceId/teamId в пути — ответ тот же, не 400")]
    public async Task GetUserWithNonNumericContext()
    {
        // Сегменты контекста в этих путях ручка не использует: пользователь берётся
        // из identity, а идентификаторы до contract-first вообще не связывались.
        // Контракт описывает их строками — иначе мусор в сегменте начал бы отбиваться
        // как 400 на связывании, чего раньше не было.
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.GetAsync("/v1/workspaces/not-a-number/teams/also-not/users");

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        AssertIsSelf(body, scenario);
    }

    [Fact(DisplayName = "PUT .../users: 200 и форма User")]
    public async Task PutUser()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.PutAsJsonAsync(
            scenario.TeamPath("/users"),
            new { name = "Новое имя" });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal("Новое имя", body.GetProperty("name").GetString());
    }

    [Fact(DisplayName = "POST .../users/batch/list: 200, массив пользователей")]
    public async Task ListUsersBatch()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.PostAsJsonAsync(
            scenario.TeamPath("/users/batch/list"),
            new[] { scenario.UserId.ToString(CultureInfo.InvariantCulture) });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        var user = Assert.Single(body.EnumerateArray().ToArray());
        AssertIsSelf(user, scenario);
    }

    [Fact(DisplayName = "GET .../users/autocomplete: 200 и форма AutocompleteUsers")]
    public async Task AutocompleteUsers()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);
        await scenario.Client.PostAsync(scenario.TeamPath("/members/join"), null);

        var response = await scenario.Client.GetAsync(
            scenario.TeamPath("/users/autocomplete?searchString=&skip=0&take=10"));

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        // Команда по умолчанию общая на прогон, поэтому в выдаче есть и соседние
        // пользователи: проверяется, что автодополнение видит вступившего.
        var users = body.GetProperty("users").EnumerateArray().ToArray();
        Assert.True(body.GetProperty("total").GetInt32() >= users.Length);
        Assert.Contains(
            users,
            user => user.GetProperty("id").GetString() == scenario.UserId.ToString(CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName = "Аватар: POST, GET content, GET {userId}/content, DELETE")]
    public async Task AvatarLifecycle()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var uploaded = await scenario.Client.PostAsync(
            scenario.TeamPath("/users/avatar"),
            ContractScenario.FileContent("avatar.png"));
        await ContractResponse.EmptyAsync(uploaded, HttpStatusCode.OK);

        var content = await scenario.Client.GetAsync(scenario.TeamPath("/users/avatar/content"));
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);

        var byId = await scenario.Client.GetAsync(scenario.TeamPath($"/users/{scenario.UserId}/avatar/content"));
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);

        // Загрузка отвечает 200, снятие — 204: асимметрия на проводе, и фронт
        // разбирает оба ответа как «тела нет».
        var deleted = await scenario.Client.DeleteAsync(scenario.TeamPath("/users/avatar"));
        await ContractResponse.EmptyAsync(deleted, HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "GET .../users/external-links: 200, массив привязок")]
    public async Task GetExternalLinks()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.GetAsync(scenario.TeamPath("/users/external-links"));

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    [Fact(DisplayName = "DELETE .../users/external-links/{provider}: последнюю привязку снять нельзя")]
    public async Task UnlinkProvider()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.DeleteAsync(
            scenario.TeamPath("/users/external-links/oidc"));

        await ContractResponse.ProblemAsync(response, "last_login_method", HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "PUT и DELETE .../users/mattermost: привязка Mattermost")]
    public async Task MattermostLink()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var linked = await scenario.Client.PutAsJsonAsync(
            scenario.TeamPath("/users/mattermost"),
            new { mattermost_user_id = "mm-" + scenario.UserId });

        // Привязка отвечает 204, снятие — 200: асимметрия на проводе, тела нет у обоих.
        await ContractResponse.EmptyAsync(linked, HttpStatusCode.NoContent);

        var unlinked = await scenario.Client.DeleteAsync(scenario.TeamPath("/users/mattermost"));
        await ContractResponse.EmptyAsync(unlinked, HttpStatusCode.OK);
    }

    [Fact(DisplayName = "POST .../users/merge: слияние с другим пользователем")]
    public async Task MergeUsers()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);
        var sourceUserId = await UsersScenario.CreateUserAsync(fixture);

        var response = await scenario.Client.PostAsJsonAsync(
            scenario.TeamPath("/users/merge"),
            new { source_user_id = sourceUserId.ToString(CultureInfo.InvariantCulture) });

        await ContractResponse.EmptyAsync(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName = "DELETE .../users: 200")]
    public async Task DeleteUser()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.DeleteAsync(scenario.TeamPath("/users"));

        await ContractResponse.EmptyAsync(response, HttpStatusCode.OK);
    }

    /// <summary>
    /// Ручки профиля отдают пользователя из identity, а не из сегментов пути.
    /// </summary>
    private static void AssertIsSelf(JsonElement user, UsersScenario scenario)
    {
        Assert.Equal(scenario.UserId.ToString(CultureInfo.InvariantCulture), user.GetProperty("id").GetString());
        Assert.False(string.IsNullOrWhiteSpace(user.GetProperty("name").GetString()));
    }
}
