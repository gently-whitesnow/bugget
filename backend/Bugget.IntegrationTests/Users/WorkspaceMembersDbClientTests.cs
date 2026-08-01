using System;
using System.Linq;
using System.Threading.Tasks;
using Bugget.Application.Users.Commands.Users;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Users;
using Bugget.IntegrationTests.Users.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests.Users;

[Collection("PostgresCollection")]
public class WorkspaceMembersDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IWorkspaceMembersDbClient _workspaceMembersDbClient;
    private readonly ITeamMembersDbClient _teamMembersDbClient;
    private readonly IUsersDbClient _usersDbClient;
    private readonly IWorkspacesDbClient _workspacesDbClient;
    private readonly ITeamsDbClient _teamsDbClient;

    public WorkspaceMembersDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _workspaceMembersDbClient = scope.ServiceProvider.GetRequiredService<IWorkspaceMembersDbClient>();
        _teamMembersDbClient = scope.ServiceProvider.GetRequiredService<ITeamMembersDbClient>();
        _usersDbClient = scope.ServiceProvider.GetRequiredService<IUsersDbClient>();
        _workspacesDbClient = scope.ServiceProvider.GetRequiredService<IWorkspacesDbClient>();
        _teamsDbClient = scope.ServiceProvider.GetRequiredService<ITeamsDbClient>();
    }

    [Fact(DisplayName = "CreateWorkspaceMemberAsync добавляет участника в воркспейс")]
    public async Task CreateWorkspaceMemberAsync_WhenValid_ShouldAddMember()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var user1 = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u1_{Guid.NewGuid()}",
            Name = "User 1",
            ImageUrl = null
        });

        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");

        var result = await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Member, 10);
        Assert.True((result.Error is null));
        var m1 = result.Value!;
        Assert.Equal(ws.Id, m1.WorkspaceId);
        Assert.Equal(user1.Id, m1.UserId);
        Assert.Equal(WorkspaceRole.Member, m1.Role);
    }

    [Fact(DisplayName = "CreateWorkspaceMemberAsync возвращает ошибку при превышении лимита")]
    public async Task CreateWorkspaceMemberAsync_WhenLimitExceeded_ShouldReturnError()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");

        // Добавляем 3 участника с лимитом 10
        for (int i = 0; i < 3; i++)
        {
            var member = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
            {
                ExternalId = $"member{i}_{Guid.NewGuid()}",
                Name = $"Member {i}",
                ImageUrl = null
            });
            var result = await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(member.Id, ws.Id, WorkspaceRole.Member, 10);
            Assert.True((result.Error is null));
        }

        // Пытаемся добавить еще одного с лимитом 3 (owner + 3 члена = 4)
        var newMember = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"newmember_{Guid.NewGuid()}",
            Name = "New Member",
            ImageUrl = null
        });

        var failResult = await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(newMember.Id, ws.Id, WorkspaceRole.Member, 3);
        Assert.False((failResult.Error is null));
        Assert.Equal(Bugget.Application.Users.Ports.WorkspaceMembersErrors.WorkspaceLimitExceededError, failResult.Error);
    }

    [Fact(DisplayName = "CreateWorkspaceMemberAsync при повторном вызове не изменяет состояния")]
    public async Task CreateWorkspaceMemberAsync_WhenValid_ShouldNotChangeState()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var user1 = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u1_{Guid.NewGuid()}",
            Name = "User 1",
            ImageUrl = null
        });

        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");

        var result = await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Admin, 10);
        Assert.True((result.Error is null));
        var m1 = result.Value!;
        Assert.Equal(ws.Id, m1.WorkspaceId);
        Assert.Equal(user1.Id, m1.UserId);
        Assert.Equal(WorkspaceRole.Admin, m1.Role);

        var result2 = await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Member, 10);
        Assert.True((result2.Error is null));
        var m2 = result2.Value!;
        Assert.Equal(ws.Id, m2.WorkspaceId);
        Assert.Equal(user1.Id, m2.UserId);
        Assert.Equal(WorkspaceRole.Admin, m2.Role);
    }

    [Fact(DisplayName = "ListWorkspacesMembersAsync возвращает участников воркспейса")]
    public async Task ListWorkspacesMembersAsync_WhenMembersExist_ShouldReturnMembers()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var user1 = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u1_{Guid.NewGuid()}",
            Name = "User 1",
            ImageUrl = null
        });
        var user2 = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u2_{Guid.NewGuid()}",
            Name = "User 2",
            ImageUrl = null
        });

        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");

        var m1Result = await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Member, 10);
        var m2Result = await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(user2.Id, ws.Id, WorkspaceRole.Admin, 10);

        Assert.True((m1Result.Error is null));
        Assert.True((m2Result.Error is null));

        var members = await _workspaceMembersDbClient.ListWorkspaceMembersAsync(ws.Id);
        Assert.Equal(3, members.Length);
        Assert.Contains(members, x => x.UserId == owner.Id && x.Role == WorkspaceRole.Admin);
        Assert.Contains(members, x => x.UserId == user1.Id && x.Role == WorkspaceRole.Member);
        Assert.Contains(members, x => x.UserId == user2.Id && x.Role == WorkspaceRole.Admin);
    }

    [Fact(DisplayName = "ListWorkspacesMembersAsync пуст для неизвестного воркспейса")]
    public async Task ListWorkspacesMembersAsync_WhenUnknownWorkspace_ShouldBeEmpty()
    {
        var members = await _workspaceMembersDbClient.ListWorkspaceMembersAsync(int.MaxValue);
        Assert.Empty(members);
    }

    [Fact(DisplayName = "ListTeamMembersAsync возвращает участников команды по team_id")]
    public async Task ListTeamMembersAsync_WhenMembersExist_ShouldReturnMembers()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");
        var team = await _teamsDbClient.CreateTeamAsync(ws.Id, "Team");

        var user = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"member_{Guid.NewGuid()}",
            Name = "Member",
            ImageUrl = null
        });

        var result = await _teamMembersDbClient.CreateTeamMemberAsync(user.Id, team.Id, 10);
        Assert.True((result.Error is null));
        Assert.Equal(team.Id, result.Value!.TeamId);

        var members = await _teamMembersDbClient.ListTeamMembersAsync(team.Id);
        Assert.Single(members);
        Assert.Equal(user.Id, members[0].UserId);
        Assert.Equal(team.Id, members[0].TeamId);
    }

    [Fact(DisplayName = "DeleteWorkspaceMemberAsync удаляет участника из воркспейса")]
    public async Task DeleteWorkspaceMemberAsync_WhenMemberExists_ShouldRemoveMember()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");

        var member = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"member_{Guid.NewGuid()}",
            Name = "Member",
            ImageUrl = null
        });

        var createResult = await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(member.Id, ws.Id, WorkspaceRole.Member, 10);
        Assert.True((createResult.Error is null));

        var beforeDelete = await _workspaceMembersDbClient.ListWorkspaceMembersAsync(ws.Id);
        Assert.Equal(2, beforeDelete.Length); // owner + member

        await _workspaceMembersDbClient.DeleteWorkspaceMemberAsync(member.Id, ws.Id);

        var afterDelete = await _workspaceMembersDbClient.ListWorkspaceMembersAsync(ws.Id);
        Assert.Single(afterDelete); // только owner
        Assert.Equal(owner.Id, afterDelete[0].UserId);
    }

    [Fact(DisplayName = "DeleteWorkspaceMemberAsync не ломается при удалении несуществующего участника")]
    public async Task DeleteWorkspaceMemberAsync_WhenMemberNotExists_ShouldNotThrow()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");

        var nonExistentUserId = 999999999L;
        await _workspaceMembersDbClient.DeleteWorkspaceMemberAsync(nonExistentUserId, ws.Id);

        var members = await _workspaceMembersDbClient.ListWorkspaceMembersAsync(ws.Id);
        Assert.Single(members); // только owner
        Assert.Equal(owner.Id, members[0].UserId);
    }

    [Fact(DisplayName = "CreateWorkspaceMemberAsync без лимита добавляет участника в воркспейс")]
    public async Task CreateWorkspaceMemberAsync_WithoutLimit_ShouldAddMember()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var user1 = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u1_{Guid.NewGuid()}",
            Name = "User 1",
            ImageUrl = null
        });

        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");

        var created = await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Member);
        Assert.Equal(ws.Id, created.WorkspaceId);
        Assert.Equal(user1.Id, created.UserId);
        Assert.Equal(WorkspaceRole.Member, created.Role);

        var members = await _workspaceMembersDbClient.ListWorkspaceMembersAsync(ws.Id);
        Assert.Equal(2, members.Length); // owner + user1
        Assert.Contains(members, x => x.UserId == user1.Id && x.Role == WorkspaceRole.Member);
    }

    [Fact(DisplayName = "CreateWorkspaceMemberAsync без лимита при повторном вызове не изменяет роль")]
    public async Task CreateWorkspaceMemberAsync_WithoutLimit_WhenDuplicate_ShouldNotChangeRole()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var user1 = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u1_{Guid.NewGuid()}",
            Name = "User 1",
            ImageUrl = null
        });

        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");

        var first = await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Admin);
        Assert.Equal(WorkspaceRole.Admin, first.Role);

        var second = await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Member);
        Assert.Equal(WorkspaceRole.Admin, second.Role); // роль не изменилась

        var members = await _workspaceMembersDbClient.ListWorkspaceMembersAsync(ws.Id);
        Assert.Equal(2, members.Length);
    }

    [Fact(DisplayName = "UpdateWorkspaceMemberAsync обновляет роль участника")]
    public async Task UpdateWorkspaceMemberAsync_WhenMemberExists_ShouldUpdateRole()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var user1 = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u1_{Guid.NewGuid()}",
            Name = "User 1",
            ImageUrl = null
        });

        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");

        var created = await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Member);
        Assert.Equal(WorkspaceRole.Member, created.Role);

        var updated = await _workspaceMembersDbClient.UpdateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Admin);
        Assert.Equal(WorkspaceRole.Admin, updated.Role);
        Assert.Equal(user1.Id, updated.UserId);
        Assert.Equal(ws.Id, updated.WorkspaceId);

        var members = await _workspaceMembersDbClient.ListWorkspaceMembersAsync(ws.Id);
        var member = members.First(x => x.UserId == user1.Id);
        Assert.Equal(WorkspaceRole.Admin, member.Role);
    }

    [Fact(DisplayName = "UpdateWorkspaceMemberAsync может понизить роль с Admin до Member")]
    public async Task UpdateWorkspaceMemberAsync_WhenAdmin_ShouldDowngradeToMember()
    {
        var owner = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var user1 = await _usersDbClient.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u1_{Guid.NewGuid()}",
            Name = "User 1",
            ImageUrl = null
        });

        var ws = await _workspacesDbClient.CreateWorkspaceAsync(owner.Id, "WS");

        await _workspaceMembersDbClient.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Admin);

        var updated = await _workspaceMembersDbClient.UpdateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Member);
        Assert.Equal(WorkspaceRole.Member, updated.Role);

        var members = await _workspaceMembersDbClient.ListWorkspaceMembersAsync(ws.Id);
        var member = members.First(x => x.UserId == user1.Id);
        Assert.Equal(WorkspaceRole.Member, member.Role);
    }
}


