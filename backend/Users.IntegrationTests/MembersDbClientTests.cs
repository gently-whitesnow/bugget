using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Users.DA.DbClients;
using Users.DA.Interfaces;
using Users.DA.TeamMembers;
using Users.Entities.BO;
using Users.Entities.Dto.Users;
using Users.IntegrationTests.Fixtures;
using Xunit;

namespace Users.IntegrationTests;

[Collection("PostgresCollection")]
public class MembersDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IMembersRepository _membersDbClient;
    private readonly IWorkspaceMembersRepository _workspaceMembersDbClient;
    private readonly ITeamMembersRepository _teamMembersDbClient;
    private readonly IUsersRepository _usersDbClient;
    private readonly ITeamsRepository _teamsDbClient;
    private readonly IWorkspacesRepository _workspacesDbClient;

    public MembersDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _membersDbClient = scope.ServiceProvider.GetRequiredService<IMembersRepository>();
        _usersDbClient = scope.ServiceProvider.GetRequiredService<IUsersRepository>();
        _teamsDbClient = scope.ServiceProvider.GetRequiredService<ITeamsRepository>();
        _workspacesDbClient = scope.ServiceProvider.GetRequiredService<IWorkspacesRepository>();
        _workspaceMembersDbClient = scope.ServiceProvider.GetRequiredService<IWorkspaceMembersRepository>();
        _teamMembersDbClient = scope.ServiceProvider.GetRequiredService<ITeamMembersRepository>();
    }

    [Fact(DisplayName = "Пустой список участий для нового пользователя")]
    public async Task ListMembersAsync_WhenNewUser_ShouldBeEmpty()
    {
        var user = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"user_{Guid.NewGuid()}",
            Name = "New User",
            ImageUrl = null
        });

        var (workspaces, teams) = await _membersDbClient.ListMembersAsync(user.Id);

        Assert.Empty(workspaces);
        Assert.Empty(teams);
    }

    [Fact(DisplayName = "Создание участника рабочей области и его получение в списке")]
    public async Task CreateWorkspaceMemberAsync_WhenValid_ShouldAppearInList()
    {
        var user = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"user_{Guid.NewGuid()}",
            Name = "Workspace Member",
            ImageUrl = null
        });

        var secondUser = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"user_{Guid.NewGuid()}",
            Name = "Second User",
            ImageUrl = null
        });

        var ws = await _workspacesDbClient.CreateWorkspaceAsync(user.Id, "Org");

        var created = await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(secondUser.Id, ws.Id, WorkspaceRole.Member, 10);

        Assert.Equal(ws.Id, created.Value!.WorkspaceId);
        Assert.Equal(WorkspaceRole.Member, created.Value!.Role);

        var (workspaces, teams) = await _membersDbClient.ListMembersAsync(secondUser.Id);
        Assert.Single(workspaces);
        Assert.Equal(ws.Id, workspaces[0].WorkspaceId);
        Assert.Equal(WorkspaceRole.Member, workspaces[0].Role);
        Assert.Empty(teams);
    }

    [Fact(DisplayName = "Создание участника команды и его получение в списке")]
    public async Task CreateTeamMemberAsync_WhenValid_ShouldAppearInList()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });

        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "Org");
        var team = await _teamsDbClient.CreateTeamAsync(ws.Id, "Org Team");

        var member = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"member_{Guid.NewGuid()}",
            Name = "Member",
            ImageUrl = null
        });

        var created = await _teamMembersDbClient.CreateTeamMemberAsync(member.Id, team.Id, 10);

        Assert.Equal(team.Id, created.Value!.TeamId);

        var (workspaces, teams) = await _membersDbClient.ListMembersAsync(member.Id);
        Assert.Empty(workspaces); // организация создана владельцу, участнику она не присваивается автоматом
        Assert.Single(teams);
        Assert.Equal(team.Id, teams[0].TeamId);
    }
}


