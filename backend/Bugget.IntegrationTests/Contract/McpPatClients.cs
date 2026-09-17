using ModelContextProtocol.Client;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// MCP-клиенты с identity вида «после /_internal/auth по PAT» и владение их транспортами:
/// общая write-поверхность агента для contract-тестов инструментов.
/// </summary>
internal sealed class McpPatClients(AppContractFixture fixture) : IAsyncDisposable
{
    private readonly List<HttpClientTransport> _transports = [];

    public async Task<McpClient> CreateAsync(ContractScenario scenario)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(fixture.BaseAddress, "/v1/mcp"),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    [ContractHeaders.UserId] = scenario.UserId,
                    [ContractHeaders.TeamId] = scenario.TeamId,
                    [ContractHeaders.WorkspaceId] = scenario.WorkspaceId,
                    [ContractHeaders.WorkspaceRole] = "owner",
                    [ContractHeaders.AuthMethod] = "pat",
                },
            },
            fixture.CreateAnonymousClient(),
            loggerFactory: null,
            ownsHttpClient: true);

        _transports.Add(transport);

        return await McpClient.CreateAsync(transport);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var transport in _transports)
        {
            await transport.DisposeAsync();
        }
    }
}
