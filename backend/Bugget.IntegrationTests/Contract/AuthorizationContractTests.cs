using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using Bugget.Api.Authorization.Interfaces;
using Bugget.Application.Users.Commands.PersonalAccessTokens;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Authentication;
using Bugget.Domain.Users;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контракт авторизации. Отдельно от остальных путей потому, что <c>/_internal/auth</c>
/// вызывает не фронт, а сам nginx через <c>auth_request</c>
/// (deploy/nginx/snippets/locations/01-authorization-api.conf:29). Его ответ — статус
/// и заголовки <c>Auth-Request-*</c> — это вход в identity для всех остальных запросов:
/// сломается он, и авторизация ляжет целиком, а не в одном экране.
/// </summary>
[Collection("PostgresCollection")]
public sealed class AuthorizationContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    [Fact(DisplayName = "GET /v1/fake/login без identity: 302, auth-cookie и рабочая сессия")]
    public async Task FakeLoginWithoutIdentity()
    {
        var client = fixture.CreateAnonymousClientWithoutRedirects();

        var response = await client.GetAsync(
            $"/v1/fake/login?externalId=contract-{Guid.NewGuid():N}&name=Contract%20User");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("http://localhost/", response.Headers.Location?.OriginalString);

        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        var accessCookie = Assert.Single(
            cookies,
            cookie => cookie.StartsWith("access_token=", StringComparison.Ordinal));
        Assert.Contains(cookies, cookie => cookie.StartsWith("refresh_token=", StringComparison.Ordinal));

        var sessionClient = fixture.CreateAnonymousClient();
        sessionClient.DefaultRequestHeaders.Add("Cookie", accessCookie.Split(';', 2)[0]);
        var sessionResponse = await sessionClient.GetAsync("/_internal/auth");
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
    }

    [Theory(DisplayName = "GET /v1/fake/login с пустым externalId: 400 без auth-cookie")]
    [InlineData("")]
    [InlineData("%20%20")]
    public async Task FakeLoginRejectsEmptyExternalId(string externalId)
    {
        var client = fixture.CreateAnonymousClientWithoutRedirects();

        var response = await client.GetAsync($"/v1/fake/login?externalId={externalId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Theory(DisplayName = "GET /v1/fake/login принимает только безопасный локальный next")]
    [InlineData("/reports/42?tab=activity", "http://localhost/reports/42?tab=activity")]
    [InlineData("https://evil.example/steal", "http://localhost/")]
    public async Task FakeLoginSanitizesNext(string next, string expectedLocation)
    {
        var client = fixture.CreateAnonymousClientWithoutRedirects();

        var response = await client.GetAsync(
            $"/v1/fake/login?externalId=next-{Guid.NewGuid():N}&next={Uri.EscapeDataString(next)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expectedLocation, response.Headers.Location?.OriginalString);
    }

    [Fact(DisplayName = "GET /_internal/auth без токена: 401 — nginx отдаст фронту 401/редирект")]
    public async Task InternalAuthWithoutToken()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/_internal/auth");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "GET /_internal/auth с валидным токеном: 200 и заголовок Auth-Request-User-Id")]
    public async Task InternalAuthWithToken()
    {
        var (client, userId) = await LoginAsync();

        var response = await client.GetAsync("/_internal/auth");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Именно этот заголовок nginx перекладывает в запрос к API
        // (auth_request_set $auth_user_id $upstream_http_auth_request_user_id).
        Assert.True(response.Headers.TryGetValues(ContractHeaders.UserId, out var values));
        Assert.Equal(userId, Assert.Single(values!));
        Assert.True(response.Headers.TryGetValues(ContractHeaders.AuthMethod, out var authMethod));
        Assert.Equal(AuthMethods.Jwt, Assert.Single(authMethod!));
    }

    [Fact(DisplayName = "GET /_internal/auth с мусорным токеном: 401")]
    public async Task InternalAuthWithBrokenToken()
    {
        var client = fixture.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");

        var response = await client.GetAsync("/_internal/auth");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "GET /_internal/auth с валидным PAT: 200, Auth-Request-* и auth_method=pat")]
    public async Task InternalAuthWithPersonalAccessToken()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);
        var generated = PersonalAccessTokenSecret.Generate();
        var tokens = fixture.Services.GetRequiredService<IPersonalAccessTokensDbClient>();
        await tokens.CreateAsync(new CreatePersonalAccessTokenDto
        {
            UserId = scenario.UserId,
            WorkspaceId = scenario.WorkspaceId,
            TeamId = scenario.TeamId,
            Label = "contract-pat",
            TokenHash = generated.Hash,
            TokenPrefix = generated.DisplayPrefix,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        });

        var client = fixture.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", generated.Value);
        client.DefaultRequestHeaders.Add(
            "X-Original-URI",
            $"/v1/workspaces/{scenario.WorkspaceId}/teams/{scenario.TeamId}/reports");

        var response = await client.GetAsync("/_internal/auth");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(ContractHeaders.UserId, out var userId));
        Assert.Equal(scenario.UserId.ToString(CultureInfo.InvariantCulture), Assert.Single(userId!));
        Assert.True(response.Headers.TryGetValues(ContractHeaders.WorkspaceId, out var workspaceId));
        Assert.Equal(scenario.WorkspaceId.ToString(CultureInfo.InvariantCulture), Assert.Single(workspaceId!));
        Assert.True(response.Headers.TryGetValues(ContractHeaders.TeamId, out var teamId));
        Assert.Equal(scenario.TeamId.ToString(CultureInfo.InvariantCulture), Assert.Single(teamId!));
        Assert.True(response.Headers.TryGetValues(ContractHeaders.AuthMethod, out var authMethod));
        Assert.Equal(AuthMethods.Pat, Assert.Single(authMethod!));

        var stored = await tokens.FindByHashAsync(generated.Hash);
        Assert.NotNull(stored);
        Assert.NotNull(stored.LastUsedAt);
    }

    [Fact(DisplayName = "GET /_internal/auth с PAT и чужим workspace/team: 401")]
    public async Task InternalAuthWithPersonalAccessTokenRejectsScopeMismatch()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);
        var generated = PersonalAccessTokenSecret.Generate();
        var tokens = fixture.Services.GetRequiredService<IPersonalAccessTokensDbClient>();
        await tokens.CreateAsync(new CreatePersonalAccessTokenDto
        {
            UserId = scenario.UserId,
            WorkspaceId = scenario.WorkspaceId,
            TeamId = scenario.TeamId,
            Label = "contract-pat-mismatch",
            TokenHash = generated.Hash,
            TokenPrefix = generated.DisplayPrefix,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        });

        var client = fixture.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", generated.Value);
        client.DefaultRequestHeaders.Add(
            "X-Original-URI",
            $"/v1/workspaces/{scenario.WorkspaceId}/teams/{scenario.TeamId + 1}/reports");

        var response = await client.GetAsync("/_internal/auth");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "GET /_internal/auth с отозванным PAT: 401")]
    public async Task InternalAuthWithRevokedPersonalAccessToken()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);
        var generated = PersonalAccessTokenSecret.Generate();
        var tokens = fixture.Services.GetRequiredService<IPersonalAccessTokensDbClient>();
        var created = await tokens.CreateAsync(new CreatePersonalAccessTokenDto
        {
            UserId = scenario.UserId,
            WorkspaceId = scenario.WorkspaceId,
            TeamId = scenario.TeamId,
            Label = "contract-pat-revoked",
            TokenHash = generated.Hash,
            TokenPrefix = generated.DisplayPrefix,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        });
        Assert.True(await tokens.RevokeAsync(created.Id, scenario.UserId));

        var client = fixture.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", generated.Value);
        client.DefaultRequestHeaders.Add(
            "X-Original-URI",
            $"/v1/workspaces/{scenario.WorkspaceId}/teams/{scenario.TeamId}/reports");

        var response = await client.GetAsync("/_internal/auth");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "GET /_internal/auth с просроченным PAT: 401")]
    public async Task InternalAuthWithExpiredPersonalAccessToken()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);
        var generated = PersonalAccessTokenSecret.Generate();
        var tokens = fixture.Services.GetRequiredService<IPersonalAccessTokensDbClient>();
        await tokens.CreateAsync(new CreatePersonalAccessTokenDto
        {
            UserId = scenario.UserId,
            WorkspaceId = scenario.WorkspaceId,
            TeamId = scenario.TeamId,
            Label = "contract-pat-expired",
            TokenHash = generated.Hash,
            TokenPrefix = generated.DisplayPrefix,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

        var client = fixture.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", generated.Value);
        client.DefaultRequestHeaders.Add(
            "X-Original-URI",
            $"/v1/workspaces/{scenario.WorkspaceId}/teams/{scenario.TeamId}/reports");

        var response = await client.GetAsync("/_internal/auth");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "POST /v1/logout: 200 и адрес редиректа в теле")]
    public async Task Logout()
    {
        var (client, _) = await LoginAsync();

        var response = await client.PostAsync("/v1/logout", null);

        // Фронт после выхода уводит браузер по этому адресу: пустой redirect_url
        // оставил бы пользователя на странице с погашенной сессией.
        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("redirect_url").GetString()));
    }

    /// <summary>
    /// Токен выпускается тем же сервисом, что и после реального входа, и кладётся в
    /// куку <c>access_token</c> — ровно так, как это делает провайдер логина
    /// (см. HttpContextExtensions.SetJsonWebTokensCookie). Пользователь при этом
    /// должен существовать в модуле users: JWT-хендлер сверяет его на каждом запросе.
    /// </summary>
    private async Task<(HttpClient Client, string UserId)> LoginAsync()
    {
        var userId = await UsersScenario.CreateUserAsync(fixture);

        var tokens = fixture.Services.GetRequiredService<ITokensService>();
        var (accessToken, _) = await tokens.GenerateTokensAsync(userId);

        var client = fixture.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("Cookie", $"access_token={accessToken}");

        return (client, userId.ToString(CultureInfo.InvariantCulture));
    }
}
