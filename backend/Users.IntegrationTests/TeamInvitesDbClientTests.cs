using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Users.BO.TeamInvites;
using Users.DA.Interfaces;
using Users.DA.TeamInvites;
using Users.DA.TeamMembers;
using Users.Entities.Dto.Users;
using Users.IntegrationTests.Fixtures;
using Xunit;

namespace Users.IntegrationTests;

[Collection("PostgresCollection")]
public class TeamInvitesDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly ITeamInvitesRepository _teamInvitesRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly IWorkspacesRepository _workspacesRepository;
    private readonly ITeamsRepository _teamsRepository;

    private readonly string pepper = "test_pepper";

    public TeamInvitesDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _teamInvitesRepository = scope.ServiceProvider.GetRequiredService<ITeamInvitesRepository>();
        _usersRepository = scope.ServiceProvider.GetRequiredService<IUsersRepository>();
        _workspacesRepository = scope.ServiceProvider.GetRequiredService<IWorkspacesRepository>();
        _teamsRepository = scope.ServiceProvider.GetRequiredService<ITeamsRepository>();
    }

    [Fact(DisplayName = "Создание инвайта для команды с валидными параметрами")]
    public async Task CreateTeamInviteAsync_WhenValid_ShouldCreateInvite()
    {
        // Arrange
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var workspace = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team = await _teamsRepository.CreateTeamAsync(workspace.Id, "Test Team");

        var tokenHash = InviteCryptoHelper.HashToken(InviteCryptoHelper.NewTokenRaw(), Encoding.UTF8.GetBytes(pepper));
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        // Act
        var invite = await _teamInvitesRepository.CreateTeamInviteAsync(workspace.Id, team.Id, tokenHash, expiresAt);

        // Assert
        Assert.NotNull(invite);
        Assert.True(invite.Id > 0);
        Assert.Equal(team.Id, invite.TeamId);
        Assert.Equal(workspace.Id, invite.WorkspaceId);
        Assert.Equal(Encoding.UTF8.GetString(tokenHash), Encoding.UTF8.GetString(invite.TokenHash));
        Assert.True(invite.CreatedAt > DateTimeOffset.MinValue);
        // timestamptz в Postgres хранит микросекунды, а DateTimeOffset.UtcNow даёт тики по
        // 100 нс, поэтому round-trip округляет значение. Сравниваем с точностью хранилища.
        Assert.Equal(expiresAt, invite.ExpiresAt, TimeSpan.FromMicroseconds(1));
    }

    [Fact(DisplayName = "Создание инвайта для той же команды обновляет существующий (ON CONFLICT)")]
    public async Task CreateTeamInviteAsync_WhenDuplicate_ShouldUpdateExisting()
    {
        // Arrange
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var workspace = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team = await _teamsRepository.CreateTeamAsync(workspace.Id, "Test Team");

        var tokenHash1 = InviteCryptoHelper.HashToken(InviteCryptoHelper.NewTokenRaw(), Encoding.UTF8.GetBytes(pepper));
        var expiresAt1 = DateTimeOffset.UtcNow.AddDays(7);

        // Act - создаем первый инвайт
        var invite1 = await _teamInvitesRepository.CreateTeamInviteAsync(workspace.Id, team.Id, tokenHash1, expiresAt1);

        // Создаем второй инвайт для той же команды (должен обновить первый)
        var tokenHash2 = InviteCryptoHelper.HashToken(InviteCryptoHelper.NewTokenRaw(), Encoding.UTF8.GetBytes(pepper));
        var expiresAt2 = DateTimeOffset.UtcNow.AddDays(14);
        var invite2 = await _teamInvitesRepository.CreateTeamInviteAsync(workspace.Id, team.Id, tokenHash2, expiresAt2);

        // Assert
        Assert.NotNull(invite2);
        Assert.Equal(Encoding.UTF8.GetString(tokenHash2), Encoding.UTF8.GetString(invite2.TokenHash));
        Assert.Equal(expiresAt2, invite2.ExpiresAt, TimeSpan.FromMicroseconds(1));
    }

    [Fact(DisplayName = "Получение инвайта для команды с инвайтом")]
    public async Task GetTeamInviteAsync_WhenInviteExists_ShouldReturnInvite()
    {
        // Arrange
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var workspace = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team = await _teamsRepository.CreateTeamAsync(workspace.Id, "Test Team");

        var tokenHash = InviteCryptoHelper.HashToken(InviteCryptoHelper.NewTokenRaw(), Encoding.UTF8.GetBytes(pepper));
        var createdInvite = await _teamInvitesRepository.CreateTeamInviteAsync(
            workspace.Id, team.Id, tokenHash, DateTimeOffset.UtcNow.AddDays(7));

        // Act
        var invite = await _teamInvitesRepository.GetTeamInviteAsync(team.Id);

        // Assert
        Assert.NotNull(invite);
        Assert.Equal(createdInvite.Id, invite!.Id);
        Assert.Equal(team.Id, invite.TeamId);
    }

    [Fact(DisplayName = "Получение инвайта для команды без инвайта возвращает null")]
    public async Task GetTeamInviteAsync_WhenNoInvite_ShouldReturnNull()
    {
        // Arrange
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var workspace = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team = await _teamsRepository.CreateTeamAsync(workspace.Id, "Empty Team");

        // Act
        var invite = await _teamInvitesRepository.GetTeamInviteAsync(team.Id);

        // Assert
        Assert.Null(invite);
    }

    [Fact(DisplayName = "Обновление инвайта с валидными параметрами")]
    public async Task UpdateTeamInviteAsync_WhenValid_ShouldUpdateInvite()
    {
        // Arrange
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var workspace = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team = await _teamsRepository.CreateTeamAsync(workspace.Id, "Test Team");

        var originalTokenHash = InviteCryptoHelper.HashToken(InviteCryptoHelper.NewTokenRaw(), Encoding.UTF8.GetBytes(pepper));
        var originalExpiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var createdInvite = await _teamInvitesRepository.CreateTeamInviteAsync(workspace.Id, team.Id, originalTokenHash, originalExpiresAt);

        var newTokenHash = InviteCryptoHelper.HashToken(InviteCryptoHelper.NewTokenRaw(), Encoding.UTF8.GetBytes(pepper));
        var newExpiresAt = DateTimeOffset.UtcNow.AddDays(14);

        // Act
        var updatedInvite = await _teamInvitesRepository.UpdateTeamInviteAsync(team.Id, createdInvite.Id, newTokenHash, newExpiresAt);

        // Assert
        Assert.NotNull(updatedInvite);
        Assert.Equal(createdInvite.Id, updatedInvite!.Id);
        Assert.Equal(team.Id, updatedInvite.TeamId);
        Assert.Equal(Encoding.UTF8.GetString(newTokenHash), Encoding.UTF8.GetString(updatedInvite.TokenHash));
        Assert.Equal(newExpiresAt, updatedInvite.ExpiresAt, TimeSpan.FromMicroseconds(1));
    }

    [Fact(DisplayName = "Обновление несуществующего инвайта возвращает null")]
    public async Task UpdateTeamInviteAsync_WhenNotFound_ShouldReturnNull()
    {
        // Arrange
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var workspace = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team = await _teamsRepository.CreateTeamAsync(workspace.Id, "Test Team");

        var nonExistentInviteId = int.MaxValue;
        var newTokenHash = InviteCryptoHelper.HashToken(InviteCryptoHelper.NewTokenRaw(), Encoding.UTF8.GetBytes(pepper));
        var newExpiresAt = DateTimeOffset.UtcNow.AddDays(7);

        // Act
        var result = await _teamInvitesRepository.UpdateTeamInviteAsync(team.Id, nonExistentInviteId, newTokenHash, newExpiresAt);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Удаление инвайта удаляет его из базы данных")]
    public async Task DeleteTeamInviteAsync_WhenValid_ShouldDeleteInvite()
    {
        // Arrange
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var workspace = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team = await _teamsRepository.CreateTeamAsync(workspace.Id, "Test Team");

        var tokenHash = InviteCryptoHelper.HashToken(InviteCryptoHelper.NewTokenRaw(), Encoding.UTF8.GetBytes(pepper));
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var createdInvite = await _teamInvitesRepository.CreateTeamInviteAsync(workspace.Id, team.Id, tokenHash, expiresAt);

        // Act
        await _teamInvitesRepository.DeleteTeamInviteAsync(team.Id, createdInvite.Id);

        // Assert
        var invite = await _teamInvitesRepository.GetTeamInviteAsync(team.Id);
        Assert.Null(invite);
    }

    [Fact(DisplayName = "Удаление несуществующего инвайта выполняется без ошибки")]
    public async Task DeleteTeamInviteAsync_WhenNotFound_ShouldNotThrow()
    {
        // Arrange
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var workspace = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team = await _teamsRepository.CreateTeamAsync(workspace.Id, "Test Team");

        var nonExistentInviteId = int.MaxValue;

        // Act & Assert
        await _teamInvitesRepository.DeleteTeamInviteAsync(team.Id, nonExistentInviteId);
        // Если метод не бросает исключение, тест пройден
    }

    [Fact(DisplayName = "Получение инвайта возвращает только инвайт конкретной команды")]
    public async Task GetTeamInviteAsync_WhenMultipleTeams_ShouldReturnOnlyForSpecificTeam()
    {
        // Arrange
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var workspace = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team1 = await _teamsRepository.CreateTeamAsync(workspace.Id, "Team 1");
        var team2 = await _teamsRepository.CreateTeamAsync(workspace.Id, "Team 2");

        var invite1 = await _teamInvitesRepository.CreateTeamInviteAsync(
            workspace.Id, team1.Id,
            InviteCryptoHelper.HashToken(InviteCryptoHelper.NewTokenRaw(), Encoding.UTF8.GetBytes(pepper)),
            DateTimeOffset.UtcNow.AddDays(7));
        var invite2 = await _teamInvitesRepository.CreateTeamInviteAsync(
            workspace.Id, team2.Id,
            InviteCryptoHelper.HashToken(InviteCryptoHelper.NewTokenRaw(), Encoding.UTF8.GetBytes(pepper)),
            DateTimeOffset.UtcNow.AddDays(7));

        // Act
        var team1Invite = await _teamInvitesRepository.GetTeamInviteAsync(team1.Id);
        var team2Invite = await _teamInvitesRepository.GetTeamInviteAsync(team2.Id);

        // Assert
        Assert.NotNull(team1Invite);
        Assert.Equal(invite1.Id, team1Invite!.Id);

        Assert.NotNull(team2Invite);
        Assert.Equal(invite2.Id, team2Invite!.Id);
    }

    [Fact(DisplayName = "Принятие валидного инвайта возвращает инвайт")]
    public async Task AcceptTeamInviteAsync_WhenValid_ShouldReturnInvite()
    {
        // Arrange
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var workspace = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team = await _teamsRepository.CreateTeamAsync(workspace.Id, "Test Team");

        var tokenHash = InviteCryptoHelper.HashToken(InviteCryptoHelper.NewTokenRaw(), Encoding.UTF8.GetBytes(pepper));
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var createdInvite = await _teamInvitesRepository.CreateTeamInviteAsync(workspace.Id, team.Id, tokenHash, expiresAt);

        // Act
        var acceptedInvite = await _teamInvitesRepository.AcceptTeamInviteAsync(tokenHash);

        // Assert
        Assert.NotNull(acceptedInvite);
        Assert.Equal(createdInvite.Id, acceptedInvite!.Id);
        Assert.Equal(team.Id, acceptedInvite.TeamId);
    }

    [Fact(DisplayName = "Принятие несуществующего инвайта возвращает null")]
    public async Task AcceptTeamInviteAsync_WhenNotFound_ShouldReturnNull()
    {
        // Arrange
        var nonExistentTokenHash = InviteCryptoHelper.HashToken(InviteCryptoHelper.NewTokenRaw(), Encoding.UTF8.GetBytes(pepper));

        // Act
        var result = await _teamInvitesRepository.AcceptTeamInviteAsync(nonExistentTokenHash);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Обновление инвайта с неверным team_id возвращает null")]
    public async Task UpdateTeamInviteAsync_WhenWrongTeamId_ShouldReturnNull()
    {
        // Arrange
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var workspace = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team1 = await _teamsRepository.CreateTeamAsync(workspace.Id, "Team 1");
        var team2 = await _teamsRepository.CreateTeamAsync(workspace.Id, "Team 2");

        var tokenHash = InviteCryptoHelper.HashToken(InviteCryptoHelper.NewTokenRaw(), Encoding.UTF8.GetBytes(pepper));
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var createdInvite = await _teamInvitesRepository.CreateTeamInviteAsync(workspace.Id, team1.Id, tokenHash, expiresAt);

        var newTokenHash = InviteCryptoHelper.HashToken(InviteCryptoHelper.NewTokenRaw(), Encoding.UTF8.GetBytes(pepper));
        var newExpiresAt = DateTimeOffset.UtcNow.AddDays(14);

        // Act - пытаемся обновить инвайт team1 используя team2.Id
        var result = await _teamInvitesRepository.UpdateTeamInviteAsync(team2.Id, createdInvite.Id, newTokenHash, newExpiresAt);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "Удаление инвайта из другой команды не удаляет инвайт")]
    public async Task DeleteTeamInviteAsync_WhenWrongTeamId_ShouldNotDeleteInvite()
    {
        // Arrange
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var workspace = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Test Workspace");
        var team1 = await _teamsRepository.CreateTeamAsync(workspace.Id, "Team 1");
        var team2 = await _teamsRepository.CreateTeamAsync(workspace.Id, "Team 2");

        var tokenHash = InviteCryptoHelper.HashToken(InviteCryptoHelper.NewTokenRaw(), Encoding.UTF8.GetBytes(pepper));
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var createdInvite = await _teamInvitesRepository.CreateTeamInviteAsync(workspace.Id, team1.Id, tokenHash, expiresAt);

        // Act - пытаемся удалить инвайт team1 используя team2.Id
        await _teamInvitesRepository.DeleteTeamInviteAsync(team2.Id, createdInvite.Id);

        // Assert - инвайт должен остаться в team1
        var team1Invite = await _teamInvitesRepository.GetTeamInviteAsync(team1.Id);
        Assert.NotNull(team1Invite);
        Assert.Equal(createdInvite.Id, team1Invite!.Id);
    }
}

