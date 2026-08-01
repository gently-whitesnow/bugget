using System;
using System.Threading.Tasks;
using Bugget.Application.Users.Commands.Users;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Users;
using Bugget.Infrastructure.Users.DbClients;
using Bugget.IntegrationTests.Users.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests.Users;

[Collection("PostgresCollection")]
public class WorkspacesDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IWorkspacesDbClient _workspacesDbClient;
    private readonly IUsersDbClient _usersDbClient;
    private readonly ITeamsDbClient _teamsDbClient;
    private readonly ITeamMembersDbClient _teamMembersDbClient;
    private readonly IWorkspaceMembersDbClient _workspaceMembersDbClient;

    public WorkspacesDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _workspacesDbClient = scope.ServiceProvider.GetRequiredService<IWorkspacesDbClient>();
        _usersDbClient = scope.ServiceProvider.GetRequiredService<IUsersDbClient>();
        _teamsDbClient = scope.ServiceProvider.GetRequiredService<ITeamsDbClient>();
        _teamMembersDbClient = scope.ServiceProvider.GetRequiredService<ITeamMembersDbClient>();
        _workspaceMembersDbClient = scope.ServiceProvider.GetRequiredService<IWorkspaceMembersDbClient>();
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
        var owner = await _usersDbClient.TryInsertUserAsync(ownerDto);

        var workspaceName = "Test Workspace";

        // Act
        var result = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, workspaceName);

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
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });

        var ws1 = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS1");
        var ws2 = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS2");

        var list = await _workspacesDbClient.ListWorkspacesAsync(owner.Id);
        Assert.True(list.Length >= 2);
        Assert.Contains(list, x => x.Id == ws1.Id && x.Name == ws1.Name);
        Assert.Contains(list, x => x.Id == ws2.Id && x.Name == ws2.Name);
    }

    [Fact(DisplayName = "DeleteWorkspaceAsync удаляет воркспейс и каскадно удаляет команды, членов воркспейса и членов команд")]
    public async Task DeleteWorkspaceAsync_ShouldCascadeDeleteTeamsWorkspaceMembersAndTeamMembers()
    {
        // Arrange - создаем воркспейс с owner
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var workspace = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "Test Workspace");

        // Добавляем дополнительных членов воркспейса
        var wsMember1 = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"ws_member1_{Guid.NewGuid()}",
            Name = "WS Member 1",
            ImageUrl = null
        });
        var wsMember2 = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"ws_member2_{Guid.NewGuid()}",
            Name = "WS Member 2",
            ImageUrl = null
        });

        await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(wsMember1.Id, workspace.Id, WorkspaceRole.Member, 10);
        await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(wsMember2.Id, workspace.Id, WorkspaceRole.Admin, 10);

        // Создаем команды в воркспейсе
        var team1 = await _teamsDbClient.CreateTeamAsync(workspace.Id, "Team 1");
        var team2 = await _teamsDbClient.CreateTeamAsync(workspace.Id, "Team 2");

        // Добавляем участников в команды
        var teamMember1 = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"team_member1_{Guid.NewGuid()}",
            Name = "Team Member 1",
            ImageUrl = null
        });
        var teamMember2 = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"team_member2_{Guid.NewGuid()}",
            Name = "Team Member 2",
            ImageUrl = null
        });

        await _teamMembersDbClient.CreateTeamMemberAsync(teamMember1.Id, team1.Id, 10);
        await _teamMembersDbClient.CreateTeamMemberAsync(teamMember2.Id, team1.Id, 10);
        await _teamMembersDbClient.CreateTeamMemberAsync(teamMember1.Id, team2.Id, 10);

        // Проверяем, что все создано
        var workspaceMembersBeforeDelete = await _workspaceMembersDbClient.ListWorkspaceMembersAsync(workspace.Id);
        var team1MembersBeforeDelete = await _teamMembersDbClient.ListTeamMembersAsync(team1.Id);
        var team2MembersBeforeDelete = await _teamMembersDbClient.ListTeamMembersAsync(team2.Id);

        Assert.Equal(3, workspaceMembersBeforeDelete.Length); // owner + 2 members
        Assert.Equal(2, team1MembersBeforeDelete.Length);
        Assert.Single(team2MembersBeforeDelete);

        // Act - удаляем воркспейс
        await _workspacesDbClient.DeleteWorkspaceAsync(workspace.Id);

        // Assert - проверяем, что воркспейс удален
        var workspacesAfterDelete = await _workspacesDbClient.ListWorkspacesAsync(owner.Id);
        Assert.DoesNotContain(workspacesAfterDelete, w => w.Id == workspace.Id);

        // Проверяем, что команды удалены (каскадное удаление)
        var teamsAfterDelete = await _teamsDbClient.ListTeamsAsync(new[] { workspace.Id });
        Assert.Empty(teamsAfterDelete);

        // Проверяем, что члены воркспейса удалены (каскадное удаление)
        var workspaceMembersAfterDelete = await _workspaceMembersDbClient.ListWorkspaceMembersAsync(workspace.Id);
        Assert.Empty(workspaceMembersAfterDelete);

        // Проверяем, что члены команд удалены (каскадное удаление через удаление команд)
        var team1MembersAfterDelete = await _teamMembersDbClient.ListTeamMembersAsync(team1.Id);
        var team2MembersAfterDelete = await _teamMembersDbClient.ListTeamMembersAsync(team2.Id);
        Assert.Empty(team1MembersAfterDelete);
        Assert.Empty(team2MembersAfterDelete);
    }

    [Fact(DisplayName = "DeleteWorkspaceAsync не ломается при удалении несуществующего воркспейса")]
    public async Task DeleteWorkspaceAsync_WhenWorkspaceNotExists_ShouldNotThrow()
    {
        var nonExistentWorkspaceId = int.MaxValue;

        await _workspacesDbClient.DeleteWorkspaceAsync(nonExistentWorkspaceId);

        // Проверяем, что операция завершилась без исключений
        Assert.True(true);
    }

    [Fact(DisplayName = "CreateWorkspaceAsync без userId успешно создает воркспейс (self-hosted режим)")]
    public async Task CreateWorkspaceAsync_WithoutUserId_ShouldCreateWorkspace()
    {
        // Arrange
        var workspaceName = "Self-Hosted Workspace";

        // Act
        var result = await _workspacesDbClient.CreateWorkspaceAsync(workspaceName);

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
        var ws1 = await _workspacesDbClient.CreateWorkspaceAsync("Global WS1");
        var ws2 = await _workspacesDbClient.CreateWorkspaceAsync("Global WS2");

        // Act
        var list = await _workspacesDbClient.ListWorkspacesAsync();

        // Assert
        Assert.True(list.Length >= 2);
        Assert.Contains(list, x => x.Id == ws1.Id && x.Name == ws1.Name);
        Assert.Contains(list, x => x.Id == ws2.Id && x.Name == ws2.Name);
    }
}
