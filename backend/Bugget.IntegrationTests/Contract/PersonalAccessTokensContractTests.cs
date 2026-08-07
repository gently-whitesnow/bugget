using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контракт personal access tokens: выпуск, список, отзыв. Ключевой инвариант
/// поверхности — значение токена существует в открытом виде только в ответе на
/// выпуск; список и любые другие ответы содержат лишь опознавательный префикс.
/// </summary>
[Collection("PostgresCollection")]
public sealed class PersonalAccessTokensContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    private const string TokensPath = "/users/personal-access-tokens";

    [Fact(DisplayName = "POST .../personal-access-tokens: 200, значение токена — только здесь и один раз")]
    public async Task CreateReturnsSecretOnce()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.PostAsJsonAsync(
            scenario.TeamPath(TokensPath),
            new { label = "mcp" });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);

        var token = body.GetProperty("token").GetString();
        Assert.NotNull(token);
        Assert.StartsWith("bgt_pat_", token);

        var record = body.GetProperty("personal_access_token");
        Assert.Equal("mcp", record.GetProperty("label").GetString());
        Assert.Equal(scenario.WorkspaceId, record.GetProperty("workspace_id").GetInt32());
        Assert.Equal(scenario.TeamId, record.GetProperty("team_id").GetInt32());
        Assert.StartsWith(record.GetProperty("token_prefix").GetString()!, token);

        // Срок по умолчанию назначен, бессрочного токена по умолчанию не бывает.
        Assert.NotEqual(JsonValueKind.Null, record.GetProperty("expires_at").ValueKind);

        // Запись о выпуске не содержит значения нигде, кроме поля token.
        Assert.DoesNotContain(token, record.GetRawText());
    }

    [Fact(DisplayName = "GET .../personal-access-tokens: 200, секрета нет ни в каком виде")]
    public async Task ListNeverContainsSecret()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);
        var created = await CreateTokenAsync(scenario, "list-check");

        var response = await scenario.Client.GetAsync(scenario.TeamPath(TokensPath));

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        var items = body.EnumerateArray().ToArray();
        var item = Assert.Single(items, i => i.GetProperty("id").GetString() == created.Id);

        Assert.Equal("list-check", item.GetProperty("label").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("last_used_at").ValueKind);

        // Весь ответ списка целиком не содержит значения токена — только префикс.
        Assert.DoesNotContain(created.Token, body.GetRawText());
        Assert.StartsWith(item.GetProperty("token_prefix").GetString()!, created.Token);
    }

    [Fact(DisplayName = "DELETE .../personal-access-tokens/{id}: 204, повторный отзыв — 404")]
    public async Task RevokeIsNotRepeatable()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);
        var created = await CreateTokenAsync(scenario, "revoke-me");

        var revoked = await scenario.Client.DeleteAsync(scenario.TeamPath($"{TokensPath}/{created.Id}"));
        await ContractResponse.EmptyAsync(revoked, HttpStatusCode.NoContent);

        var again = await scenario.Client.DeleteAsync(scenario.TeamPath($"{TokensPath}/{created.Id}"));
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);

        // Отозванный токен пропадает из списка.
        var list = await ContractResponse.JsonAsync(
            await scenario.Client.GetAsync(scenario.TeamPath(TokensPath)), HttpStatusCode.OK);
        Assert.DoesNotContain(list.EnumerateArray(), i => i.GetProperty("id").GetString() == created.Id);
    }

    [Fact(DisplayName = "DELETE чужого токена: 404, токен остаётся действующим")]
    public async Task RevokeForeignTokenIsNotFound()
    {
        var owner = await UsersScenario.CreateAsync(fixture);
        var stranger = await UsersScenario.CreateAsync(fixture);
        var created = await CreateTokenAsync(owner, "foreign");

        var response = await stranger.Client.DeleteAsync(stranger.TeamPath($"{TokensPath}/{created.Id}"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var list = await ContractResponse.JsonAsync(
            await owner.Client.GetAsync(owner.TeamPath(TokensPath)), HttpStatusCode.OK);
        Assert.Contains(list.EnumerateArray(), i => i.GetProperty("id").GetString() == created.Id);
    }

    [Fact(DisplayName = "POST без команды в identity: 404 — токен не к чему привязать")]
    public async Task CreateWithoutTeamIsNotFound()
    {
        var userId = await UsersScenario.CreateUserAsync(fixture);
        var client = fixture.CreateAuthorizedClient("0", "0", userId.ToString());

        var response = await client.PostAsJsonAsync(
            $"/v1/workspaces/0/teams/0{TokensPath}",
            new { label = "no-team" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "DELETE с нечисловым tokenId: 404 от ограничения маршрута")]
    public async Task RevokeWithNonNumericIdIsNotFound()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);

        var response = await scenario.Client.DeleteAsync(scenario.TeamPath($"{TokensPath}/not-a-number"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "POST с явным expires_at: срок сохраняется как запрошен")]
    public async Task CreateKeepsRequestedExpiry()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);
        var requested = DateTimeOffset.UtcNow.AddDays(7);

        var response = await scenario.Client.PostAsJsonAsync(
            scenario.TeamPath(TokensPath),
            new { label = "short-lived", expires_at = requested });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        var expiresAt = body.GetProperty("personal_access_token").GetProperty("expires_at").GetDateTimeOffset();
        Assert.Equal(requested, expiresAt, TimeSpan.FromSeconds(1));
    }

    private sealed record CreatedToken(string Id, string Token);

    private static async Task<CreatedToken> CreateTokenAsync(UsersScenario scenario, string label)
    {
        var response = await scenario.Client.PostAsJsonAsync(
            scenario.TeamPath(TokensPath),
            new { label });
        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);

        return new CreatedToken(
            body.GetProperty("personal_access_token").GetProperty("id").GetString()!,
            body.GetProperty("token").GetString()!);
    }
}
