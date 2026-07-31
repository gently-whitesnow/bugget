using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Users.BO.Ports;
using Users.Entities.BO;
using Users.Entities.Dto.Users;
using Users.IntegrationTests.Fixtures;
using Xunit;

namespace Users.IntegrationTests;

[Collection("PostgresCollection")]
public class WorkspaceMembersDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IWorkspaceMembersRepository _workspaceMembersRepository;
    private readonly ITeamMembersRepository _teamMembersRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly IWorkspacesRepository _workspacesRepository;
    private readonly ITeamsRepository _teamsRepository;

    public WorkspaceMembersDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _workspaceMembersRepository = scope.ServiceProvider.GetRequiredService<IWorkspaceMembersRepository>();
        _teamMembersRepository = scope.ServiceProvider.GetRequiredService<ITeamMembersRepository>();
        _usersRepository = scope.ServiceProvider.GetRequiredService<IUsersRepository>();
        _workspacesRepository = scope.ServiceProvider.GetRequiredService<IWorkspacesRepository>();
        _teamsRepository = scope.ServiceProvider.GetRequiredService<ITeamsRepository>();
    }

    [Fact(DisplayName = "CreateWorkspaceMemberAsync добавляет участника в воркспейс")]
    public async Task CreateWorkspaceMemberAsync_WhenValid_ShouldAddMember()
    {
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var user1 = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u1_{Guid.NewGuid()}",
            Name = "User 1",
            ImageUrl = null
        });

        var ws = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS");

        var result = await _workspaceMembersRepository.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Member, 10);
        Assert.True((result.Error is null));
        var m1 = result.Value!;
        Assert.Equal(ws.Id, m1.WorkspaceId);
        Assert.Equal(user1.Id, m1.UserId);
        Assert.Equal(WorkspaceRole.Member, m1.Role);
    }

    [Fact(DisplayName = "CreateWorkspaceMemberAsync возвращает ошибку при превышении лимита")]
    public async Task CreateWorkspaceMemberAsync_WhenLimitExceeded_ShouldReturnError()
    {
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS");

        // Добавляем 3 участника с лимитом 10
        for (int i = 0; i < 3; i++)
        {
            var member = await _usersRepository.TryInsertUserAsync(new CreateUserDto
            {
                ExternalId = $"member{i}_{Guid.NewGuid()}",
                Name = $"Member {i}",
                ImageUrl = null
            });
            var result = await _workspaceMembersRepository.CreateWorkspaceMemberAsync(member.Id, ws.Id, WorkspaceRole.Member, 10);
            Assert.True((result.Error is null));
        }

        // Пытаемся добавить еще одного с лимитом 3 (owner + 3 члена = 4)
        var newMember = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"newmember_{Guid.NewGuid()}",
            Name = "New Member",
            ImageUrl = null
        });

        var failResult = await _workspaceMembersRepository.CreateWorkspaceMemberAsync(newMember.Id, ws.Id, WorkspaceRole.Member, 3);
        Assert.False((failResult.Error is null));
        Assert.Equal(Users.BO.Ports.WorkspaceMembersErrors.WorkspaceLimitExceededError, failResult.Error);
    }

    [Fact(DisplayName = "CreateWorkspaceMemberAsync при повторном вызове не изменяет состояния")]
    public async Task CreateWorkspaceMemberAsync_WhenValid_ShouldNotChangeState()
    {
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var user1 = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u1_{Guid.NewGuid()}",
            Name = "User 1",
            ImageUrl = null
        });

        var ws = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS");

        var result = await _workspaceMembersRepository.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Admin, 10);
        Assert.True((result.Error is null));
        var m1 = result.Value!;
        Assert.Equal(ws.Id, m1.WorkspaceId);
        Assert.Equal(user1.Id, m1.UserId);
        Assert.Equal(WorkspaceRole.Admin, m1.Role);

        var result2 = await _workspaceMembersRepository.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Member, 10);
        Assert.True((result2.Error is null));
        var m2 = result2.Value!;
        Assert.Equal(ws.Id, m2.WorkspaceId);
        Assert.Equal(user1.Id, m2.UserId);
        Assert.Equal(WorkspaceRole.Admin, m2.Role);
    }

    [Fact(DisplayName = "ListWorkspacesMembersAsync возвращает участников воркспейса")]
    public async Task ListWorkspacesMembersAsync_WhenMembersExist_ShouldReturnMembers()
    {
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var user1 = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u1_{Guid.NewGuid()}",
            Name = "User 1",
            ImageUrl = null
        });
        var user2 = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u2_{Guid.NewGuid()}",
            Name = "User 2",
            ImageUrl = null
        });

        var ws = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS");

        var m1Result = await _workspaceMembersRepository.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Member, 10);
        var m2Result = await _workspaceMembersRepository.CreateWorkspaceMemberAsync(user2.Id, ws.Id, WorkspaceRole.Admin, 10);

        Assert.True((m1Result.Error is null));
        Assert.True((m2Result.Error is null));

        var members = await _workspaceMembersRepository.ListWorkspaceMembersAsync(ws.Id);
        Assert.Equal(3, members.Length);
        Assert.Contains(members, x => x.UserId == owner.Id && x.Role == WorkspaceRole.Admin);
        Assert.Contains(members, x => x.UserId == user1.Id && x.Role == WorkspaceRole.Member);
        Assert.Contains(members, x => x.UserId == user2.Id && x.Role == WorkspaceRole.Admin);
    }

    [Fact(DisplayName = "ListWorkspacesMembersAsync пуст для неизвестного воркспейса")]
    public async Task ListWorkspacesMembersAsync_WhenUnknownWorkspace_ShouldBeEmpty()
    {
        var members = await _workspaceMembersRepository.ListWorkspaceMembersAsync(int.MaxValue);
        Assert.Empty(members);
    }

    [Fact(DisplayName = "ListTeamMembersAsync возвращает участников команды по team_id")]
    public async Task ListTeamMembersAsync_WhenMembersExist_ShouldReturnMembers()
    {
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS");
        var team = await _teamsRepository.CreateTeamAsync(ws.Id, "Team");

        var user = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"member_{Guid.NewGuid()}",
            Name = "Member",
            ImageUrl = null
        });

        var result = await _teamMembersRepository.CreateTeamMemberAsync(user.Id, team.Id, 10);
        Assert.True((result.Error is null));
        Assert.Equal(team.Id, result.Value!.TeamId);

        var members = await _teamMembersRepository.ListTeamMembersAsync(team.Id);
        Assert.Single(members);
        Assert.Equal(user.Id, members[0].UserId);
        Assert.Equal(team.Id, members[0].TeamId);
    }

    [Fact(DisplayName = "DeleteWorkspaceMemberAsync удаляет участника из воркспейса")]
    public async Task DeleteWorkspaceMemberAsync_WhenMemberExists_ShouldRemoveMember()
    {
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS");

        var member = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"member_{Guid.NewGuid()}",
            Name = "Member",
            ImageUrl = null
        });

        var createResult = await _workspaceMembersRepository.CreateWorkspaceMemberAsync(member.Id, ws.Id, WorkspaceRole.Member, 10);
        Assert.True((createResult.Error is null));

        var beforeDelete = await _workspaceMembersRepository.ListWorkspaceMembersAsync(ws.Id);
        Assert.Equal(2, beforeDelete.Length); // owner + member

        await _workspaceMembersRepository.DeleteWorkspaceMemberAsync(member.Id, ws.Id);

        var afterDelete = await _workspaceMembersRepository.ListWorkspaceMembersAsync(ws.Id);
        Assert.Single(afterDelete); // только owner
        Assert.Equal(owner.Id, afterDelete[0].UserId);
    }

    [Fact(DisplayName = "DeleteWorkspaceMemberAsync не ломается при удалении несуществующего участника")]
    public async Task DeleteWorkspaceMemberAsync_WhenMemberNotExists_ShouldNotThrow()
    {
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS");

        var nonExistentUserId = 999999999L;
        await _workspaceMembersRepository.DeleteWorkspaceMemberAsync(nonExistentUserId, ws.Id);

        var members = await _workspaceMembersRepository.ListWorkspaceMembersAsync(ws.Id);
        Assert.Single(members); // только owner
        Assert.Equal(owner.Id, members[0].UserId);
    }

    [Fact(DisplayName = "CreateWorkspaceMemberAsync без лимита добавляет участника в воркспейс")]
    public async Task CreateWorkspaceMemberAsync_WithoutLimit_ShouldAddMember()
    {
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var user1 = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u1_{Guid.NewGuid()}",
            Name = "User 1",
            ImageUrl = null
        });

        var ws = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS");

        var created = await _workspaceMembersRepository.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Member);
        Assert.Equal(ws.Id, created.WorkspaceId);
        Assert.Equal(user1.Id, created.UserId);
        Assert.Equal(WorkspaceRole.Member, created.Role);

        var members = await _workspaceMembersRepository.ListWorkspaceMembersAsync(ws.Id);
        Assert.Equal(2, members.Length); // owner + user1
        Assert.Contains(members, x => x.UserId == user1.Id && x.Role == WorkspaceRole.Member);
    }

    [Fact(DisplayName = "CreateWorkspaceMemberAsync без лимита при повторном вызове не изменяет роль")]
    public async Task CreateWorkspaceMemberAsync_WithoutLimit_WhenDuplicate_ShouldNotChangeRole()
    {
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var user1 = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u1_{Guid.NewGuid()}",
            Name = "User 1",
            ImageUrl = null
        });

        var ws = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS");

        var first = await _workspaceMembersRepository.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Admin);
        Assert.Equal(WorkspaceRole.Admin, first.Role);

        var second = await _workspaceMembersRepository.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Member);
        Assert.Equal(WorkspaceRole.Admin, second.Role); // роль не изменилась

        var members = await _workspaceMembersRepository.ListWorkspaceMembersAsync(ws.Id);
        Assert.Equal(2, members.Length);
    }

    [Fact(DisplayName = "UpdateWorkspaceMemberAsync обновляет роль участника")]
    public async Task UpdateWorkspaceMemberAsync_WhenMemberExists_ShouldUpdateRole()
    {
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var user1 = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u1_{Guid.NewGuid()}",
            Name = "User 1",
            ImageUrl = null
        });

        var ws = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS");

        var created = await _workspaceMembersRepository.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Member);
        Assert.Equal(WorkspaceRole.Member, created.Role);

        var updated = await _workspaceMembersRepository.UpdateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Admin);
        Assert.Equal(WorkspaceRole.Admin, updated.Role);
        Assert.Equal(user1.Id, updated.UserId);
        Assert.Equal(ws.Id, updated.WorkspaceId);

        var members = await _workspaceMembersRepository.ListWorkspaceMembersAsync(ws.Id);
        var member = members.First(x => x.UserId == user1.Id);
        Assert.Equal(WorkspaceRole.Admin, member.Role);
    }

    [Fact(DisplayName = "UpdateWorkspaceMemberAsync может понизить роль с Admin до Member")]
    public async Task UpdateWorkspaceMemberAsync_WhenAdmin_ShouldDowngradeToMember()
    {
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var user1 = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"u1_{Guid.NewGuid()}",
            Name = "User 1",
            ImageUrl = null
        });

        var ws = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS");

        await _workspaceMembersRepository.CreateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Admin);

        var updated = await _workspaceMembersRepository.UpdateWorkspaceMemberAsync(user1.Id, ws.Id, WorkspaceRole.Member);
        Assert.Equal(WorkspaceRole.Member, updated.Role);

        var members = await _workspaceMembersRepository.ListWorkspaceMembersAsync(ws.Id);
        var member = members.First(x => x.UserId == user1.Id);
        Assert.Equal(WorkspaceRole.Member, member.Role);
    }
}


