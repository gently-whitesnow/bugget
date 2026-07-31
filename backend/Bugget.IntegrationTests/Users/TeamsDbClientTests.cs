using System;
using System.Linq;
using System.Threading.Tasks;
using Bugget.Application.Users.Ports;
using Bugget.Contracts.Users.Dto.Teams;
using Bugget.Contracts.Users.Dto.Users;
using Bugget.Infrastructure.Users.DbClients;
using Bugget.IntegrationTests.Users.Fixtures;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bugget.IntegrationTests.Users;

[Collection("PostgresCollection")]
public class TeamsDbClientTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly ITeamsRepository _teamsRepository;
    private readonly IWorkspacesRepository _workspacesRepository;
    private readonly IUsersRepository _usersRepository;

    public TeamsDbClientTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _usersRepository = scope.ServiceProvider.GetRequiredService<IUsersRepository>();
        _teamsRepository = scope.ServiceProvider.GetRequiredService<ITeamsRepository>();
        _workspacesRepository = scope.ServiceProvider.GetRequiredService<IWorkspacesRepository>();
    }

    [Fact(DisplayName = "Создание обычной команды в существующей организации")]
    public async Task CreateTeamAsync_WhenValidOrganization_ShouldCreateEmptyTeam()
    {
        // Arrange
        var createUserDto = new CreateUserDto
        {
            ExternalId = $"org_owner_{Guid.NewGuid()}",
            Name = "Organization Owner",
            ImageUrl = "https://example.com/owner.jpg"
        };

        var ownerUser = await _usersRepository.TryInsertUserAsync(createUserDto);
        var workspace = await _workspacesRepository.CreateWorkspaceAsync(ownerUser.Id, "Main Workspace");

        // Act
        var result = await _teamsRepository.CreateTeamAsync(workspace.Id, "Main Team");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(workspace.Id, result.WorkspaceId);
        Assert.Equal("Main Team", result.Name);
        Assert.True(result.CreatedAt > DateTimeOffset.MinValue);
        Assert.True(result.UpdatedAt > DateTimeOffset.MinValue);
    }

    [Fact(DisplayName = "ListTeamsAsync возвращает команды по воркспейсам")]
    public async Task ListTeamsAsync_WhenTeamsExist_ShouldReturnByWorkspace()
    {
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws1 = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS1");
        var ws2 = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS2");

        var t11 = await _teamsRepository.CreateTeamAsync(ws1.Id, "T11");
        var t12 = await _teamsRepository.CreateTeamAsync(ws1.Id, "T12");
        var t21 = await _teamsRepository.CreateTeamAsync(ws2.Id, "T21");

        var all = await _teamsRepository.ListTeamsAsync(new[] { ws1.Id, ws2.Id });
        Assert.True(all.Length >= 3);
        Assert.Contains(all, x => x.Id == t11.Id && x.WorkspaceId == ws1.Id);
        Assert.Contains(all, x => x.Id == t12.Id && x.WorkspaceId == ws1.Id);
        Assert.Contains(all, x => x.Id == t21.Id && x.WorkspaceId == ws2.Id);

        var onlyWs1 = await _teamsRepository.ListTeamsAsync(new[] { ws1.Id });
        Assert.True(onlyWs1.Length >= 2);
        Assert.All(onlyWs1, x => Assert.Equal(ws1.Id, x.WorkspaceId));
    }

    [Fact(DisplayName = "ListTeamsAsync по workspaceId и teamIds возвращает только указанные команды")]
    public async Task ListTeamsAsync_ByWorkspaceAndTeamIds_ShouldReturnOnlyMatchingTeams()
    {
        // Arrange
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws1 = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS1");
        var ws2 = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS2");

        var t1 = await _teamsRepository.CreateTeamAsync(ws1.Id, "Team A");
        var t2 = await _teamsRepository.CreateTeamAsync(ws1.Id, "Team B");
        var t3 = await _teamsRepository.CreateTeamAsync(ws1.Id, "Team C");
        var t4 = await _teamsRepository.CreateTeamAsync(ws2.Id, "Team D");

        // Act — запрашиваем только t1 и t3 из ws1
        var result = await _teamsRepository.ListTeamsAsync(ws1.Id, new[] { t1.Id, t3.Id });

        // Assert
        Assert.Equal(2, result.Length);
        Assert.Contains(result, x => x.Id == t1.Id && x.Name == "Team A");
        Assert.Contains(result, x => x.Id == t3.Id && x.Name == "Team C");
        Assert.DoesNotContain(result, x => x.Id == t2.Id);
        Assert.All(result, x => Assert.Equal(ws1.Id, x.WorkspaceId));
    }

    [Fact(DisplayName = "ListTeamsAsync по workspaceId и teamIds не возвращает команды из чужого workspace")]
    public async Task ListTeamsAsync_ByWorkspaceAndTeamIds_ShouldNotReturnTeamsFromOtherWorkspace()
    {
        // Arrange
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws1 = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS1");
        var ws2 = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS2");

        var t1 = await _teamsRepository.CreateTeamAsync(ws1.Id, "Team in WS1");
        var t2 = await _teamsRepository.CreateTeamAsync(ws2.Id, "Team in WS2");

        // Act — передаём id команды из ws2, но запрашиваем по ws1
        var result = await _teamsRepository.ListTeamsAsync(ws1.Id, new[] { t1.Id, t2.Id });

        // Assert — должна вернуться только команда из ws1
        Assert.Single(result);
        Assert.Equal(t1.Id, result[0].Id);
        Assert.Equal(ws1.Id, result[0].WorkspaceId);
    }

    [Fact(DisplayName = "ListTeamsAsync по workspaceId и пустому массиву teamIds возвращает пустой результат")]
    public async Task ListTeamsAsync_ByWorkspaceAndEmptyTeamIds_ShouldReturnEmpty()
    {
        // Arrange
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "WS");
        await _teamsRepository.CreateTeamAsync(ws.Id, "Some Team");

        // Act
        var result = await _teamsRepository.ListTeamsAsync(ws.Id, Array.Empty<int>());

        // Assert
        Assert.Empty(result);
    }

    [Fact(DisplayName = "AutocompleteTeamsAsync возвращает команды только текущего workspace")]
    public async Task AutocompleteTeamsAsync_ShouldReturnOnlyWorkspaceTeams()
    {
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws1 = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Workspace 1");
        var ws2 = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Workspace 2");

        var w1Team = await _teamsRepository.CreateTeamAsync(ws1.Id, "Core Platform");
        await _teamsRepository.CreateTeamAsync(ws1.Id, "Core Backend");
        var w2Team = await _teamsRepository.CreateTeamAsync(ws2.Id, "Core Mobile");

        var result = await _teamsRepository.AutocompleteTeamsAsync(ws1.Id, "core", 0, 10);

        Assert.Contains(result, t => t.Id == w1Team.Id);
        Assert.DoesNotContain(result, t => t.Id == w2Team.Id);
        Assert.All(result, t => Assert.Equal(ws1.Id, t.WorkspaceId));
    }

    [Fact(DisplayName = "AutocompleteTeamsAsync учитывает pagination")]
    public async Task AutocompleteTeamsAsync_ShouldRespectPagination()
    {
        var owner = await _usersRepository.TryInsertUserAsync(new CreateUserDto
        {
            ExternalId = $"owner_{Guid.NewGuid()}",
            Name = "Owner",
            ImageUrl = null
        });
        var ws = await _workspacesRepository.CreateWorkspaceAsync(owner.Id, "Workspace");

        await _teamsRepository.CreateTeamAsync(ws.Id, "Alpha");
        await _teamsRepository.CreateTeamAsync(ws.Id, "Beta");
        await _teamsRepository.CreateTeamAsync(ws.Id, "Gamma");

        var page = await _teamsRepository.AutocompleteTeamsAsync(ws.Id, string.Empty, 1, 1);

        Assert.Single(page);
    }

}
