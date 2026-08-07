using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bugget.Application.Users.Commands.PersonalAccessTokens;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Users;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контракт MCP-эндпоинта <c>/v1/mcp</c>: transport поднят, защищён той же
/// header-trust схемой, что и остальной модуль reports. Happy path повторяет боевую
/// цепочку целиком, только без nginx: PAT → <c>/_internal/auth</c> → заголовки
/// <c>Auth-Request-*</c> → настоящий MCP-клиент делает initialize и tools/list.
/// </summary>
[Collection("PostgresCollection")]
public sealed class McpEndpointContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    [Fact(DisplayName = "MCP-клиент с identity из PAT: initialize и tools/list проходят")]
    public async Task McpClientInitializesAndListsToolsWithPatIdentity()
    {
        var scenario = await UsersScenario.CreateAsync(fixture);
        var patValue = await IssuePatAsync(scenario);

        // То, что в бою делает nginx auth_request: Bearer PAT уходит на /_internal/auth,
        // обратно приходят заголовки identity для реального запроса.
        var authClient = fixture.CreateAnonymousClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", patValue);
        authClient.DefaultRequestHeaders.Add(
            "X-Original-URI",
            $"/api/app/workspaces/{scenario.WorkspaceId}/teams/{scenario.TeamId}/v1/mcp");
        var authResponse = await authClient.GetAsync("/_internal/auth");
        Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);

        var identityHeaders = new Dictionary<string, string>();
        foreach (var name in new[]
                 {
                     ContractHeaders.UserId, ContractHeaders.WorkspaceId,
                     ContractHeaders.TeamId, ContractHeaders.WorkspaceRole,
                     ContractHeaders.AuthMethod,
                 })
        {
            if (authResponse.Headers.TryGetValues(name, out var values))
            {
                identityHeaders[name] = values.Single();
            }
        }

        Assert.Contains(ContractHeaders.UserId, identityHeaders.Keys);

        await using var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(fixture.BaseAddress, "/v1/mcp"),
                AdditionalHeaders = identityHeaders,
            },
            fixture.CreateAnonymousClient(),
            loggerFactory: null,
            ownsHttpClient: true);

        // CreateAsync — это initialize + initialized: рукопожатие протокола целиком.
        await using var mcpClient = await McpClient.CreateAsync(transport);

        Assert.Equal("bugget-api", mcpClient.ServerInfo.Name);

        // Каркас без tools: список обязан быть пустым, а не ошибкой метода.
        var tools = await mcpClient.ListToolsAsync();
        Assert.Empty(tools);
    }

    [Fact(DisplayName = "POST /v1/mcp без identity-заголовков: 401, как у остального модуля reports")]
    public async Task McpWithoutIdentityIsUnauthorized()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync(
            "/v1/mcp",
            new { jsonrpc = "2.0", id = 1, method = "initialize" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// PAT заводится через порт, а не через HTTP-ручку выпуска: её контракт — предмет
    /// PersonalAccessTokensContractTests, дублировать его здесь незачем.
    /// </summary>
    private async Task<string> IssuePatAsync(UsersScenario scenario)
    {
        var generated = PersonalAccessTokenSecret.Generate();
        var tokens = fixture.Services.GetRequiredService<IPersonalAccessTokensDbClient>();
        await tokens.CreateAsync(new CreatePersonalAccessTokenDto
        {
            UserId = scenario.UserId,
            WorkspaceId = scenario.WorkspaceId,
            TeamId = scenario.TeamId,
            Label = "mcp-contract",
            TokenHash = generated.Hash,
            TokenPrefix = generated.DisplayPrefix,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        });

        return generated.Value;
    }
}
