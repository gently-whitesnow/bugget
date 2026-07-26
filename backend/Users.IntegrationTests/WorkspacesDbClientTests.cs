using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Users.DA.DbClients;
using Users.DA.Interfaces;
using Users.DA.TeamMembers;
using Users.Entities.BO;
using Users.Entities.Dto.Users;
using Users.IntegrationTests.Fixtures;
using Xunit;

namespace Users.IntegrationTests;

[Collection("PostgresCollection")]
public class WorkspacesDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IWorkspacesRepository _workspacesRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly ITeamsRepository _teamsRepository;
    private readonly ITeamMembersRepository _teamMembersRepository;
    private readonly IWorkspaceMembersRepository _workspaceMembersRepository;

    public WorkspacesDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _workspacesRepository = scope.ServiceProvider.GetRequiredService<IWorkspacesRepository>();
        _usersRepository = scope.ServiceProvider.GetRequiredService<IUsersRepository>();
        _teamsRepository = scope.ServiceProvider.GetRequiredService<ITeamsRepository>();
        _teamMembersRepository = scope.ServiceProvider.GetRequiredService<ITeamMembersRepository>();
        _workspaceMembersRepository = scope.ServiceProvider.GetRequiredService<IWorkspaceMembersRepository>();
    }

    [Fact(DisplayName = "Успешное создание воркспейса")]
    public async Task CreateWorkspaceAsync_WhenValidData_ShouldCreateWorkspace()
    {
        // Arrange
        var ownerDto = new CreateUserDto
        {
            ExternalId = $"workspace_owner_{Guid.NewGuid()}",
            Name = "Workspace Owner",
            ImageUrl = "https://example.com/owner.jpg"
        };
        var owner = await _usersRepository.TryInsertUserAsync(ownerDto);

        var workspaceName = "Test Workspace";

        // Act
        var result = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, workspaceName);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(workspaceName, result.Name);
        Assert.True(result.CreatedAt > DateTimeOffset.MinValue);
        Assert.True(result.UpdatedAt > DateTimeOffset.MinValue);
    }

    [Fact(DisplayName = "ListWorkspacesAsync возвращает созданные воркспейсы пользователя")]
    public async Task ListWorkspacesAsync_WhenCreated_ShouldReturnAll()
    {
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });

        var ws1 = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS1");
        var ws2 = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS2");

        var list = await _workspacesRepository.ListWorkspacesAsync(owner.Id);
        Assert.True(list.Length >= 2);
        Assert.Contains(list, x => x.Id == ws1.Id && x.Name == ws1.Name);
        Assert.Contains(list, x => x.Id == ws2.Id && x.Name == ws2.Name);
    }

    [Fact(DisplayName = "DeleteWorkspaceAsync удаляет воркспейс и каскадно удаляет команды, членов воркспейса и членов команд")]
    public async Task DeleteWorkspaceAsync_ShouldCascadeDeleteTeamsWorkspaceMembersAndTeamMembers()
    {
        // Arrange - создаем воркспейс с owner
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var workspace = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Test Workspace");

        // Добавляем дополнительных членов воркспейса
        var wsMember1 = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"ws_member1_{Guid.NewGuid()}",
            Name = "WS Member 1",
            ImageUrl = null
        });
        var wsMember2 = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"ws_member2_{Guid.NewGuid()}",
            Name = "WS Member 2",
            ImageUrl = null
        });

        await _workspaceMembersRepository.CreateWorkspaceMemberAsync(wsMember1.Id, workspace.Id, WorkspaceRole.Member, 10);
        await _workspaceMembersRepository.CreateWorkspaceMemberAsync(wsMember2.Id, workspace.Id, WorkspaceRole.Admin, 10);

        // Создаем команды в воркспейсе
        var team1 = await _teamsRepository.CreateTeamAsync(workspace.Id, "Team 1");
        var team2 = await _teamsRepository.CreateTeamAsync(workspace.Id, "Team 2");

        // Добавляем участников в команды
        var teamMember1 = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"team_member1_{Guid.NewGuid()}",
            Name = "Team Member 1",
            ImageUrl = null
        });
        var teamMember2 = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"team_member2_{Guid.NewGuid()}",
            Name = "Team Member 2",
            ImageUrl = null
        });

        await _teamMembersRepository.CreateTeamMemberAsync(teamMember1.Id, team1.Id, 10);
        await _teamMembersRepository.CreateTeamMemberAsync(teamMember2.Id, team1.Id, 10);
        await _teamMembersRepository.CreateTeamMemberAsync(teamMember1.Id, team2.Id, 10);

        // Проверяем, что все создано
        var workspaceMembersBeforeDelete = await _workspaceMembersRepository.ListWorkspaceMembersAsync(workspace.Id);
        var team1MembersBeforeDelete = await _teamMembersRepository.ListTeamMembersAsync(team1.Id);
        var team2MembersBeforeDelete = await _teamMembersRepository.ListTeamMembersAsync(team2.Id);

        Assert.Equal(3, workspaceMembersBeforeDelete.Length); // owner + 2 members
        Assert.Equal(2, team1MembersBeforeDelete.Length);
        Assert.Single(team2MembersBeforeDelete);

        // Act - удаляем воркспейс
        await _workspacesRepository.DeleteWorkspaceAsync(workspace.Id);

        // Assert - проверяем, что воркспейс удален
        var workspacesAfterDelete = await _workspacesRepository.ListWorkspacesAsync(owner.Id);
        Assert.DoesNotContain(workspacesAfterDelete, w => w.Id == workspace.Id);

        // Проверяем, что команды удалены (каскадное удаление)
        var teamsAfterDelete = await _teamsRepository.ListTeamsAsync(new[] { workspace.Id });
        Assert.Empty(teamsAfterDelete);

        // Проверяем, что члены воркспейса удалены (каскадное удаление)
        var workspaceMembersAfterDelete = await _workspaceMembersRepository.ListWorkspaceMembersAsync(workspace.Id);
        Assert.Empty(workspaceMembersAfterDelete);

        // Проверяем, что члены команд удалены (каскадное удаление через удаление команд)
        var team1MembersAfterDelete = await _teamMembersRepository.ListTeamMembersAsync(team1.Id);
        var team2MembersAfterDelete = await _teamMembersRepository.ListTeamMembersAsync(team2.Id);
        Assert.Empty(team1MembersAfterDelete);
        Assert.Empty(team2MembersAfterDelete);
    }

    [Fact(DisplayName = "DeleteWorkspaceAsync не ломается при удалении несуществующего воркспейса")]
    public async Task DeleteWorkspaceAsync_WhenWorkspaceNotExists_ShouldNotThrow()
    {
        var nonExistentWorkspaceId = int.MaxValue;

        await _workspacesRepository.DeleteWorkspaceAsync(nonExistentWorkspaceId);

        // Проверяем, что операция завершилась без исключений
        Assert.True(true);
    }

    [Fact(DisplayName = "CreateWorkspaceAsync без userId успешно создает воркспейс (self-hosted режим)")]
    public async Task CreateWorkspaceAsync_WithoutUserId_ShouldCreateWorkspace()
    {
        // Arrange
        var workspaceName = "Self-Hosted Workspace";

        // Act
        var result = await _workspacesRepository.CreateWorkspaceAsync(workspaceName);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(workspaceName, result.Name);
        Assert.True(result.CreatedAt > DateTimeOffset.MinValue);
        Assert.True(result.UpdatedAt > DateTimeOffset.MinValue);
    }

    [Fact(DisplayName = "ListWorkspacesAsync без userId возвращает все воркспейсы (self-hosted режим)")]
    public async Task ListWorkspacesAsync_WithoutUserId_ShouldReturnAllWorkspaces()
    {
        // Arrange - создаем воркспейсы в self-hosted режиме
        var ws1 = await _workspacesRepository.CreateWorkspaceAsync("Global WS1");
        var ws2 = await _workspacesRepository.CreateWorkspaceAsync("Global WS2");

        // Act
        var list = await _workspacesRepository.ListWorkspacesAsync();

        // Assert
        Assert.True(list.Length >= 2);
        Assert.Contains(list, x => x.Id == ws1.Id && x.Name == ws1.Name);
        Assert.Contains(list, x => x.Id == ws2.Id && x.Name == ws2.Name);
    }
}
