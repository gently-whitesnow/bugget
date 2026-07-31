using System.Globalization;
using Bugget.Application.Users.Interfaces;
using Bugget.Contracts.Users.Dto.Users;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контекст модуля users, собранный тем же путём, что и bootstrap фронта в self-hosted
/// сборке: пользователь появляется после входа, рабочее пространство и команда уже
/// созданы на старте, дальше пользователь в них вступает
/// (см. frontend/src/shared/api/selfHosted.ts).
/// </summary>
/// <remarks>
/// Идентификатор пользователя здесь числовой: модуль users читает его как
/// <c>long</c>, в отличие от модуля reports, которому годится любая строка.
/// </remarks>
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

        // Вступление в рабочее пространство: первый участник получает роль admin.
        var joined = await bootstrapClient.PostAsync($"/v1/workspaces/{workspaceId}/members/join", null);
        await EnsureSuccessAsync(joined, "POST /v1/workspaces/{workspaceId}/members/join");

        var client = fixture.CreateAuthorizedClient(
            workspaceId.ToString(CultureInfo.InvariantCulture),
            teamId.ToString(CultureInfo.InvariantCulture),
            user,
            role);

        return new UsersScenario(client, userId, workspaceId, teamId);
    }

    /// <summary>
    /// Пользователь заводится тем же путём, что и в бою: модуль authorization после
    /// успешного входа зовёт <see cref="IUsersService.TryInsertUserAsync"/> напрямую
    /// (см. Bugget/Modules/InProcess/AuthorizationUsersClientAdapter.cs).
    /// </summary>
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
    /// Рабочее пространство и команда по умолчанию, созданные на старте
    /// <c>WorkspaceInitializationService</c>. Через API их не видно: пока пользователь
    /// не вступил, <c>GET /v1/workspaces</c> возвращает пустой список, а во фронте id
    /// приходит из ссылки-приглашения. Поэтому id читаются прямо из БД — это подготовка
    /// данных, а не проверка контракта.
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
