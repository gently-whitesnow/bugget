using System;
using System.Linq;
using System.Threading.Tasks;
using Bugget.Application.Users.Commands.Users;
using Bugget.Application.Users.Ports;
using Bugget.IntegrationTests.Users.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests.Users;

[Collection("PostgresCollection")]
public class TeamMembersDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly ITeamMembersDbClient _teamMembersDbClient;
    private readonly IUsersDbClient _usersDbClient;
    private readonly IWorkspacesDbClient _workspacesDbClient;
    private readonly ITeamsDbClient _teamsDbClient;

    public TeamMembersDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _teamMembersDbClient = scope.ServiceProvider.GetRequiredService<ITeamMembersDbClient>();
        _usersDbClient = scope.ServiceProvider.GetRequiredService<IUsersDbClient>();
        _workspacesDbClient = scope.ServiceProvider.GetRequiredService<IWorkspacesDbClient>();
        _teamsDbClient = scope.ServiceProvider.GetRequiredService<ITeamsDbClient>();
    }

    [Fact(DisplayName = "CreateTeamMemberAsync добавляет участника в команду и его видно в списке")]
    public async Task CreateTeamMemberAsync_WhenValid_ShouldBeListable()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");
        var team = await _teamsDbClient.CreateTeamAsync(ws.Id, "A");

        var member = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"member_{Guid.NewGuid()}",
            Name = "Member",
            ImageUrl = null
        });

        var result = await _teamMembersDbClient.CreateTeamMemberAsync(member.Id, team.Id, 10);
        Assert.True((result.Error is null));
        var created = result.Value!;
        Assert.Equal(team.Id, created.TeamId);
        Assert.Equal(member.Id, created.UserId);

        var list = await _teamMembersDbClient.ListTeamMembersAsync(team.Id);
        Assert.Single(list);
        Assert.Equal(team.Id, list[0].TeamId);
        Assert.Equal(member.Id, list[0].UserId);
    }

    [Fact(DisplayName = "CreateTeamMemberAsync возвращает ошибку при превышении лимита")]
    public async Task CreateTeamMemberAsync_WhenLimitExceeded_ShouldReturnError()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");
        var team = await _teamsDbClient.CreateTeamAsync(ws.Id, "Team");

        // Добавляем 3 участника с лимитом 10
        for (int i = 0; i < 3; i++)
        {
            var member = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
            {
                ExternalId = $"member{i}_{Guid.NewGuid()}",
                Name = $"Member {i}",
                ImageUrl = null
            });
            var result = await _teamMembersDbClient.CreateTeamMemberAsync(member.Id, team.Id, 10);
            Assert.True((result.Error is null));
        }

        // Пытаемся добавить еще одного с лимитом 2 (а уже есть 3)
        var newMember = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"newmember_{Guid.NewGuid()}",
            Name = "New Member",
            ImageUrl = null
        });

        var failResult = await _teamMembersDbClient.CreateTeamMemberAsync(newMember.Id, team.Id, 2);
        Assert.False((failResult.Error is null));
        Assert.Equal(Bugget.Application.Users.Ports.TeamMembersErrors.TeamLimitExceededError, failResult.Error);
    }

    [Fact(DisplayName = "ListTeamMembersAsync по нескольким командам возвращает корректные элементы")]
    public async Task ListTeamMembersAsync_WithMultipleTeams_ShouldReturnCombined()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");
        var team1 = await _teamsDbClient.CreateTeamAsync(ws.Id, "T1");
        var team2 = await _teamsDbClient.CreateTeamAsync(ws.Id, "T2");

        var m1 = await _usersDbClient.TryInsertUserAsync(new CreateUserDto { ExternalId = $"m1_{Guid.NewGuid()}", Name = "M1", ImageUrl = null });
        var m2 = await _usersDbClient.TryInsertUserAsync(new CreateUserDto { ExternalId = $"m2_{Guid.NewGuid()}", Name = "M2", ImageUrl = null });

        await _teamMembersDbClient.CreateTeamMemberAsync(m1.Id, team1.Id, 10);
        await _teamMembersDbClient.CreateTeamMemberAsync(m2.Id, team2.Id, 10);

        var list = await _teamMembersDbClient.ListTeamMembersAsync(team1.Id);
        Assert.Single(list);
        Assert.Equal(team1.Id, list[0].TeamId);
        Assert.Equal(m1.Id, list[0].UserId);
    }

    [Fact(DisplayName = "DeleteTeamMemberAsync удаляет участника из команды")]
    public async Task DeleteTeamMemberAsync_WhenMemberExists_ShouldRemoveMember()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");
        var team = await _teamsDbClient.CreateTeamAsync(ws.Id, "Team");

        var member = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"member_{Guid.NewGuid()}",
            Name = "Member",
            ImageUrl = null
        });

        var createResult = await _teamMembersDbClient.CreateTeamMemberAsync(member.Id, team.Id, 10);
        Assert.True((createResult.Error is null));

        var beforeDelete = await _teamMembersDbClient.ListTeamMembersAsync(team.Id);
        Assert.Single(beforeDelete);

        await _teamMembersDbClient.DeleteTeamMemberAsync(member.Id, team.Id);

        var afterDelete = await _teamMembersDbClient.ListTeamMembersAsync(team.Id);
        Assert.Empty(afterDelete);
    }

    [Fact(DisplayName = "DeleteTeamMemberAsync не ломается при удалении несуществующего участника")]
    public async Task DeleteTeamMemberAsync_WhenMemberNotExists_ShouldNotThrow()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");
        var team = await _teamsDbClient.CreateTeamAsync(ws.Id, "Team");

        var nonExistentUserId = 999999999L;
        await _teamMembersDbClient.DeleteTeamMemberAsync(nonExistentUserId, team.Id);

        var members = await _teamMembersDbClient.ListTeamMembersAsync(team.Id);
        Assert.Empty(members);
    }

    [Fact(DisplayName = "CreateTeamMemberAsync без лимита добавляет участника в команду")]
    public async Task CreateTeamMemberAsync_WithoutLimit_ShouldAddMember()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");
        var team = await _teamsDbClient.CreateTeamAsync(ws.Id, "Team");

        var member = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"member_{Guid.NewGuid()}",
            Name = "Member",
            ImageUrl = null
        });

        var created = await _teamMembersDbClient.CreateTeamMemberAsync(member.Id, team.Id);
        Assert.Equal(team.Id, created.TeamId);
        Assert.Equal(member.Id, created.UserId);

        var list = await _teamMembersDbClient.ListTeamMembersAsync(team.Id);
        Assert.Single(list);
        Assert.Equal(member.Id, list[0].UserId);
    }

    [Fact(DisplayName = "CreateTeamMemberAsync без лимита при повторном вызове возвращает существующего участника")]
    public async Task CreateTeamMemberAsync_WithoutLimit_WhenDuplicate_ShouldReturnExisting()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");
        var team = await _teamsDbClient.CreateTeamAsync(ws.Id, "Team");

        var member = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"member_{Guid.NewGuid()}",
            Name = "Member",
            ImageUrl = null
        });

        var first = await _teamMembersDbClient.CreateTeamMemberAsync(member.Id, team.Id);
        var second = await _teamMembersDbClient.CreateTeamMemberAsync(member.Id, team.Id);

        Assert.Equal(first.TeamId, second.TeamId);
        Assert.Equal(first.UserId, second.UserId);

        var list = await _teamMembersDbClient.ListTeamMembersAsync(team.Id);
        Assert.Single(list);
    }

    [Fact(DisplayName = "ListTeamsMemberAsync возвращает пустой массив для пользователя без команд")]
    public async Task ListTeamsMemberAsync_WhenUserInNoTeams_ShouldReturnEmpty()
    {
        var user = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"user_{Guid.NewGuid()}",
            Name = "User",
            ImageUrl = null
        });

        var list = await _teamMembersDbClient.ListTeamsMemberAsync(user.Id);
        Assert.Empty(list);
    }

    [Fact(DisplayName = "ListTeamsMemberAsync возвращает команду, в которой состоит пользователь")]
    public async Task ListTeamsMemberAsync_WhenUserInOneTeam_ShouldReturnThatTeam()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");
        var team = await _teamsDbClient.CreateTeamAsync(ws.Id, "Team");

        var member = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"member_{Guid.NewGuid()}",
            Name = "Member",
            ImageUrl = null
        });

        await _teamMembersDbClient.CreateTeamMemberAsync(member.Id, team.Id, 10);

        var list = await _teamMembersDbClient.ListTeamsMemberAsync(member.Id);
        Assert.Single(list);
        Assert.Equal(team.Id, list[0].TeamId);
        Assert.Equal(member.Id, list[0].UserId);
    }

    [Fact(DisplayName = "ListTeamsMemberAsync возвращает все команды пользователя при участии в нескольких")]
    public async Task ListTeamsMemberAsync_WhenUserInMultipleTeams_ShouldReturnAllTeams()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");
        var team1 = await _teamsDbClient.CreateTeamAsync(ws.Id, "T1");
        var team2 = await _teamsDbClient.CreateTeamAsync(ws.Id, "T2");

        var member = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"member_{Guid.NewGuid()}",
            Name = "Member",
            ImageUrl = null
        });

        await _teamMembersDbClient.CreateTeamMemberAsync(member.Id, team1.Id, 10);
        await _teamMembersDbClient.CreateTeamMemberAsync(member.Id, team2.Id, 10);

        var list = await _teamMembersDbClient.ListTeamsMemberAsync(member.Id);
        Assert.Equal(2, list.Length);
        var teamIds = list.Select(x => x.TeamId).OrderBy(x => x).ToArray();
        Assert.Contains(team1.Id, teamIds);
        Assert.Contains(team2.Id, teamIds);
        Assert.True(list.All(x => x.UserId == member.Id));
    }
}
