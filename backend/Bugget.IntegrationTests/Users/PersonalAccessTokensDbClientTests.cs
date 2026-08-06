using System;
using System.Linq;
using System.Threading.Tasks;
using Bugget.Application.Users.Commands.PersonalAccessTokens;
using Bugget.Application.Users.Commands.Users;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Users;
using Bugget.IntegrationTests.Users.Fixtures;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests.Users;

[Collection("PostgresCollection")]
public class PersonalAccessTokensDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IPersonalAccessTokensDbClient _tokensDbClient;
    private readonly IUsersDbClient _usersDbClient;
    private readonly ITeamsDbClient _teamsDbClient;
    private readonly IWorkspacesDbClient _workspacesDbClient;

    public PersonalAccessTokensDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _tokensDbClient = scope.ServiceProvider.GetRequiredService<IPersonalAccessTokensDbClient>();
        _usersDbClient = scope.ServiceProvider.GetRequiredService<IUsersDbClient>();
        _teamsDbClient = scope.ServiceProvider.GetRequiredService<ITeamsDbClient>();
        _workspacesDbClient = scope.ServiceProvider.GetRequiredService<IWorkspacesDbClient>();
    }

    [Fact(DisplayName = "Выпуск токена возвращает запись без секрета")]
    public async Task CreateAsync_ReturnsRecordWithoutSecret()
    {
        var (user, workspaceId, teamId) = await ArrangeScopeAsync();
        var generated = PersonalAccessTokenSecret.Generate();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        var token = await _tokensDbClient.CreateAsync(
            NewTokenDto(user.Id, workspaceId, teamId, generated, "mcp", expiresAt));

        Assert.True(token.Id > 0);
        Assert.Equal(user.Id, token.UserId);
        Assert.Equal(workspaceId, token.WorkspaceId);
        Assert.Equal(teamId, token.TeamId);
        Assert.Equal("mcp", token.Label);
        Assert.Equal(generated.DisplayPrefix, token.TokenPrefix);
        Assert.Null(token.RevokedAt);
        Assert.Null(token.LastUsedAt);
        Assert.NotNull(token.ExpiresAt);
        Assert.Equal(expiresAt, token.ExpiresAt.Value, TimeSpan.FromSeconds(1));
    }

    [Fact(DisplayName = "В БД не остаётся значения токена — только его хэш")]
    public async Task CreateAsync_StoresHashAndNeverThePlaintextSecret()
    {
        var (user, workspaceId, teamId) = await ArrangeScopeAsync();
        var generated = PersonalAccessTokenSecret.Generate();

        var created = await _tokensDbClient.CreateAsync(
            NewTokenDto(user.Id, workspaceId, teamId, generated));

        await using var conn = new NpgsqlConnection(
            Environment.GetEnvironmentVariable(Constants.PostgresConnectionStringEnv));
        await conn.OpenAsync();

        // Вся строка целиком приводится к тексту: значение токена не должно найтись ни в
        // одной колонке, включая открытый префикс.
        var rowContainsSecret = await conn.QuerySingleAsync<bool>(
            "SELECT pat::text LIKE '%' || @secret || '%' FROM personal_access_tokens pat WHERE pat.id = @id",
            new { secret = generated.Value, id = created.Id });
        Assert.False(rowContainsSecret);

        var storedHash = await conn.QuerySingleAsync<byte[]>(
            "SELECT token_hash FROM personal_access_tokens WHERE id = @id",
            new { id = created.Id });
        Assert.Equal(PersonalAccessTokenSecret.ComputeHash(generated.Value), storedHash);
    }

    [Fact(DisplayName = "Поиск по хэшу находит выпущенный токен")]
    public async Task FindByHashAsync_FindsIssuedToken()
    {
        var (user, workspaceId, teamId) = await ArrangeScopeAsync();
        var generated = PersonalAccessTokenSecret.Generate();
        var created = await _tokensDbClient.CreateAsync(
            NewTokenDto(user.Id, workspaceId, teamId, generated));

        var found = await _tokensDbClient.FindByHashAsync(
            PersonalAccessTokenSecret.ComputeHash(generated.Value));

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
        Assert.True(found.IsUsable(DateTimeOffset.UtcNow));
    }

    [Fact(DisplayName = "Поиск по чужому хэшу ничего не возвращает")]
    public async Task FindByHashAsync_WhenUnknownHash_ReturnsNull()
    {
        var found = await _tokensDbClient.FindByHashAsync(
            PersonalAccessTokenSecret.ComputeHash(PersonalAccessTokenSecret.Generate().Value));

        Assert.Null(found);
    }

    [Fact(DisplayName = "Просроченный токен находится, но непригоден")]
    public async Task FindByHashAsync_WhenExpired_ReturnsUnusableToken()
    {
        var (user, workspaceId, teamId) = await ArrangeScopeAsync();
        var generated = PersonalAccessTokenSecret.Generate();
        await _tokensDbClient.CreateAsync(NewTokenDto(
            user.Id, workspaceId, teamId, generated, expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1)));

        var found = await _tokensDbClient.FindByHashAsync(
            PersonalAccessTokenSecret.ComputeHash(generated.Value));

        Assert.NotNull(found);
        Assert.False(found.IsUsable(DateTimeOffset.UtcNow));
    }

    [Fact(DisplayName = "Список отдаёт только действующие токены пользователя")]
    public async Task ListAsync_ReturnsOnlyOwnActiveTokens()
    {
        var (user, workspaceId, teamId) = await ArrangeScopeAsync();
        var (otherUser, otherWorkspaceId, otherTeamId) = await ArrangeScopeAsync();

        var kept = await _tokensDbClient.CreateAsync(NewTokenDto(
            user.Id, workspaceId, teamId, PersonalAccessTokenSecret.Generate(), "kept"));
        var revoked = await _tokensDbClient.CreateAsync(NewTokenDto(
            user.Id, workspaceId, teamId, PersonalAccessTokenSecret.Generate(), "revoked"));
        await _tokensDbClient.CreateAsync(NewTokenDto(
            otherUser.Id, otherWorkspaceId, otherTeamId, PersonalAccessTokenSecret.Generate(), "foreign"));

        await _tokensDbClient.RevokeAsync(revoked.Id, user.Id);

        var tokens = await _tokensDbClient.ListAsync(user.Id);

        Assert.Equal([kept.Id], tokens.Select(t => t.Id).ToArray());
    }

    [Fact(DisplayName = "Отзыв делает токен непригодным и не повторяется")]
    public async Task RevokeAsync_MakesTokenUnusableAndIsNotRepeatable()
    {
        var (user, workspaceId, teamId) = await ArrangeScopeAsync();
        var generated = PersonalAccessTokenSecret.Generate();
        var created = await _tokensDbClient.CreateAsync(
            NewTokenDto(user.Id, workspaceId, teamId, generated));

        Assert.True(await _tokensDbClient.RevokeAsync(created.Id, user.Id));
        Assert.False(await _tokensDbClient.RevokeAsync(created.Id, user.Id));

        var found = await _tokensDbClient.FindByHashAsync(
            PersonalAccessTokenSecret.ComputeHash(generated.Value));

        Assert.NotNull(found);
        Assert.True(found.IsRevoked);
        Assert.False(found.IsUsable(DateTimeOffset.UtcNow));
    }

    [Fact(DisplayName = "Чужой токен отозвать нельзя")]
    public async Task RevokeAsync_WhenForeignOwner_DoesNothing()
    {
        var (owner, workspaceId, teamId) = await ArrangeScopeAsync();
        var (stranger, _, _) = await ArrangeScopeAsync();
        var created = await _tokensDbClient.CreateAsync(NewTokenDto(
            owner.Id, workspaceId, teamId, PersonalAccessTokenSecret.Generate()));

        Assert.False(await _tokensDbClient.RevokeAsync(created.Id, stranger.Id));

        var tokens = await _tokensDbClient.ListAsync(owner.Id);
        Assert.Contains(tokens, t => t.Id == created.Id);
    }

    [Fact(DisplayName = "Отметка об использовании проставляет last_used_at")]
    public async Task TouchLastUsedAsync_SetsLastUsedAt()
    {
        var (user, workspaceId, teamId) = await ArrangeScopeAsync();
        var generated = PersonalAccessTokenSecret.Generate();
        var created = await _tokensDbClient.CreateAsync(
            NewTokenDto(user.Id, workspaceId, teamId, generated));

        await _tokensDbClient.TouchLastUsedAsync(created.Id);

        var found = await _tokensDbClient.FindByHashAsync(
            PersonalAccessTokenSecret.ComputeHash(generated.Value));

        Assert.NotNull(found);
        Assert.NotNull(found.LastUsedAt);
    }

    private static CreatePersonalAccessTokenDto NewTokenDto(
        long userId,
        int workspaceId,
        int teamId,
        GeneratedPersonalAccessToken generated,
        string label = "test",
        DateTimeOffset? expiresAt = null) =>
        new()
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            TeamId = teamId,
            Label = label,
            TokenHash = generated.Hash,
            TokenPrefix = generated.DisplayPrefix,
            ExpiresAt = expiresAt
        };

    private async Task<(User User, int WorkspaceId, int TeamId)> ArrangeScopeAsync()
    {
        var user = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"pat_user_{Guid.NewGuid()}",
            Name = "PAT User"
        });
        var workspace = await _workspacesDbClient.CreateWorkspaceAsync(user.Id, $"PAT WS {Guid.NewGuid():N}"[..32]);
        var team = await _teamsDbClient.CreateTeamAsync(workspace.Id, $"PAT Team {Guid.NewGuid():N}"[..32]);

        return (user, workspace.Id, team.Id);
    }
}
