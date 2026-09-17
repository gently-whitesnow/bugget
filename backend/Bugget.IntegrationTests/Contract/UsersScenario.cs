using System.Globalization;
using Bugget.Application.Users.Commands.Users;
using Bugget.Application.Users.Interfaces;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контекст модуля users, собранный путём self-hosted bootstrap фронта (frontend/src/shared/api/selfHosted.ts):
/// вход, затем вступление в созданные на старте workspace и команду. Id пользователя числовой: users читает <c>long</c>.
/// </summary>
internal sealed class UsersScenario
{
    private UsersScenario(HttpClient client, long userId, int workspaceId, int teamId)
    {
        Client = client;
        UserId = userId;
        WorkspaceId = workspaceId;
        TeamId = teamId;
    }

    public HttpClient Client { get; }

    public long UserId { get; }

    public int WorkspaceId { get; }

    public int TeamId { get; }

    public static async Task<UsersScenario> CreateAsync(AppContractFixture fixture, string role = "admin")
    {
        var userId = await CreateUserAsync(fixture);
        var user = userId.ToString(CultureInfo.InvariantCulture);

        var bootstrapClient = fixture.CreateAuthorizedClient("0", "0", user);
        var (workspaceId, teamId) = await ReadDefaultContextAsync();

        var joined = await bootstrapClient.PostAsync($"/v1/workspaces/{workspaceId}/members/join", null);
        await EnsureSuccessAsync(joined, "POST /v1/workspaces/{workspaceId}/members/join");

        var client = fixture.CreateAuthorizedClient(
            workspaceId.ToString(CultureInfo.InvariantCulture),
            teamId.ToString(CultureInfo.InvariantCulture),
            user,
            role);

        return new UsersScenario(client, userId, workspaceId, teamId);
    }

    /// <summary>Как в бою: authorization после входа зовёт <see cref="IUsersService.TryInsertUserAsync"/> напрямую.</summary>
    public static async Task<long> CreateUserAsync(AppContractFixture fixture)
    {
        var usersService = fixture.Services.GetRequiredService<IUsersService>();
        var user = await usersService.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = Guid.NewGuid().ToString("N"),
            Name = "Контрактный пользователь"
        });

        return user.Id;
    }

    public string TeamPath(string suffix) =>
        $"/v1/workspaces/{WorkspaceId}/teams/{TeamId}{suffix}";

    /// <summary>
    /// Workspace и команда по умолчанию создаются на старте и через API не видны, пока пользователь не вступил
    /// (во фронте id приходит из приглашения), поэтому id читаются из БД — это подготовка данных, а не проверка контракта.
    /// </summary>
    private static async Task<(int WorkspaceId, int TeamId)> ReadDefaultContextAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("USERS_POSTGRES_CONNECTION_STRING")!;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var workspaceId = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT id FROM workspaces ORDER BY id LIMIT 1;");
        Assert.True(
            workspaceId.HasValue,
            "в self-hosted режиме рабочее пространство по умолчанию создаётся на старте — его нет");

        var teamId = await connection.QuerySingleAsync<int>(
            "SELECT id FROM teams WHERE workspace_id = @workspaceId ORDER BY id LIMIT 1;",
            new { workspaceId });

        return (workspaceId!.Value, teamId);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string what)
    {
        if (!response.IsSuccessStatusCode)
        {
            Assert.Fail($"{what} вернул {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }
}
