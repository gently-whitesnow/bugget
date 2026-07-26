using System.Net.Http.Json;
using MattermostOAuth.Models;
using Microsoft.Extensions.Options;

namespace MattermostOAuth;

public sealed class MattermostOAuthClient(
    IHttpClientFactory httpClientFactory,
    IOptions<MattermostOAuthOptions> options)
{
    private readonly MattermostOAuthOptions _options = options.Value;

    public async Task<string> ExchangeCodeAsync(string code, string redirectUri)
    {
        var httpClient = httpClientFactory.CreateClient("MattermostOAuth");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri,
        });

        var response = await httpClient.PostAsync(
            $"{_options.BaseUrl.TrimEnd('/')}/oauth/access_token", content);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return tokenResponse?.AccessToken
            ?? throw new InvalidOperationException("Failed to get access token from Mattermost");
    }

    public async Task<MattermostUserResponse> GetCurrentUserAsync(string accessToken)
    {
        var httpClient = httpClientFactory.CreateClient("MattermostOAuth");
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_options.BaseUrl.TrimEnd('/')}/api/v4/users/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MattermostUserResponse>()
            ?? throw new InvalidOperationException("Failed to get user from Mattermost");
    }

    private sealed class TokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }
}
