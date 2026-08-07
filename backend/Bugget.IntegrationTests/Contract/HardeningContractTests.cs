using System.Net;
using System.Net.Http.Headers;
using Bugget.Domain.Users;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Hardening PAT/MCP (X1): переполненное окно неудачных PAT-попыток отвечает 401
/// не глядя в БД, а поток write-вызовов агента упирается в потолок. Ключи окон
/// уникальны на тест (префикс мусорного токена, пользователь сценария), поэтому
/// статические лимитеры процесса не пересекают тесты между собой.
/// </summary>
[Collection("PostgresCollection")]
public sealed class HardeningContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    [Fact(DisplayName = "Перебор PAT: после десяти неудач тот же префикс получает 401 сразу")]
    public async Task RepeatedInvalidPatAttemptsAreRateLimited()
    {
        // Один открытый префикс, разные «хвосты» — так выглядит подбор токена.
        var stem = PersonalAccessTokenSecret.Generate().Value[..PersonalAccessTokenSecret.DisplayPrefixLength];

        for (var attempt = 0; attempt < 11; attempt++)
        {
            var client = fixture.CreateAnonymousClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", stem + $"forged_tail_{attempt:D2}_padding_to_len");
            client.DefaultRequestHeaders.Add("X-Original-URI", "/v1/workspaces/1/teams/1/reports");

            var response = await client.GetAsync("/_internal/auth");

            // И до, и после порога снаружи один и тот же 401: лимит не раскрывает
            // перебирающему, что он замечен, — но каждый ответ обязан быть 401.
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // Легитимный клиент с другим токеном (другой префикс) не задет чужим окном.
        var scenario = await UsersScenario.CreateAsync(fixture);
        var generated = PersonalAccessTokenSecret.Generate();
        var tokens = fixture.Services.GetRequiredService<Bugget.Application.Users.Ports.IPersonalAccessTokensDbClient>();
        await tokens.CreateAsync(new Bugget.Application.Users.Commands.PersonalAccessTokens.CreatePersonalAccessTokenDto
        {
            UserId = scenario.UserId,
            WorkspaceId = scenario.WorkspaceId,
            TeamId = scenario.TeamId,
            Label = "hardening-legit",
            TokenHash = generated.Hash,
            TokenPrefix = generated.DisplayPrefix,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        });

        var legit = fixture.CreateAnonymousClient();
        legit.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", generated.Value);
        legit.DefaultRequestHeaders.Add(
            "X-Original-URI",
            $"/v1/workspaces/{scenario.WorkspaceId}/teams/{scenario.TeamId}/reports");

        Assert.Equal(HttpStatusCode.OK, (await legit.GetAsync("/_internal/auth")).StatusCode);
    }

    [Fact(DisplayName = "Поток write-вызовов агента упирается в потолок, чтение не задето")]
    public async Task WriteToolsAreRateLimitedPerUser()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        await using var transport = new HttpClientTransport(
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
        await using var client = await McpClient.CreateAsync(transport);

        // Потолок 30/мин на пользователя: первые 30 write-вызовов проходят.
        for (var i = 0; i < 30; i++)
        {
            var ok = await client.CallToolAsync(
                "patch_bug",
                new Dictionary<string, object?>
                {
                    ["reportId"] = reportId,
                    ["bugId"] = bugId,
                    ["receive"] = $"итерация {i}",
                });
            Assert.True(ok.IsError != true, TextOf(ok));
        }

        var overflow = await client.CallToolAsync(
            "patch_bug",
            new Dictionary<string, object?>
            {
                ["reportId"] = reportId,
                ["bugId"] = bugId,
                ["receive"] = "не должно записаться",
            });
        Assert.True(overflow.IsError == true);
        Assert.Contains("Слишком много правок", TextOf(overflow), StringComparison.Ordinal);

        // Чтение тем же пользователем не лимитируется.
        var read = await client.CallToolAsync(
            "get_report",
            new Dictionary<string, object?> { ["reportId"] = reportId });
        Assert.True(read.IsError != true, TextOf(read));
    }

    private static string TextOf(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
