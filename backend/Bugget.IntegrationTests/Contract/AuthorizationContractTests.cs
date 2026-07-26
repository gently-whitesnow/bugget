using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using Authorization.Api.Interfaces;
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
    }

    [Fact(DisplayName = "GET /_internal/auth с мусорным токеном: 401")]
    public async Task InternalAuthWithBrokenToken()
    {
        var client = fixture.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");

        var response = await client.GetAsync("/_internal/auth");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "GET /_internal/anon/auth: 200, device id в заголовке для nginx")]
    public async Task AnonymousAuth()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/_internal/anon/auth");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(ContractHeaders.UserId, out _));
        Assert.True(response.Headers.TryGetValues("X-Auth-Set-Cookie-Device-Id", out _));
    }

    [Fact(DisplayName = "GET /v1/auth: 200 и форма AuthUser")]
    public async Task Me()
    {
        var (client, _) = await LoginAsync();

        var response = await client.GetAsync("/v1/auth");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("authorization.v1.auth.get", response);
    }

    [Fact(DisplayName = "GET /v1/auth без токена: 401")]
    public async Task MeWithoutToken()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/v1/auth");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "GET /v1/flags: 200 и форма Flags")]
    public async Task Flags()
    {
        var (client, _) = await LoginAsync();

        var response = await client.GetAsync("/v1/flags");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("authorization.v1.flags.get", response);
    }

    [Fact(DisplayName = "POST /v1/logout: 200 и адрес редиректа в теле")]
    public async Task Logout()
    {
        var (client, _) = await LoginAsync();

        var response = await client.PostAsync("/v1/logout", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("authorization.v1.logout.post", response);
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
