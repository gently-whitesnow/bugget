using Bugget.Application.Users;
using Bugget.Application.Users.Options;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Bugget.UnitTests.Users;

public class TeamsServiceTests
{
    private readonly Mock<ITeamsDbClient> _teamsDbClient;
    private readonly Mock<ITeamMembersDbClient> _teamMembersDbClient;
    private readonly Mock<IUserCacheInvalidator> _userCacheInvalidator;
    private readonly Mock<ILogger<TeamsService>> _logger;
    private readonly TeamsService _sut;
    private readonly Mock<IOptions<TeamsOptions>> _options;
    private readonly Mock<IOptions<SelfHostedOptions>> _selfHostedOptions;
    private readonly int _defaultSizeLimit = 10;
    public TeamsServiceTests()
    {
        _teamsDbClient = new Mock<ITeamsDbClient>(MockBehavior.Strict);
        _teamMembersDbClient = new Mock<ITeamMembersDbClient>(MockBehavior.Strict);
        _userCacheInvalidator = new Mock<IUserCacheInvalidator>(MockBehavior.Strict);
        _logger = new Mock<ILogger<TeamsService>>(MockBehavior.Loose);
        _options = new Mock<IOptions<TeamsOptions>>(MockBehavior.Strict);
        _options.Setup(o => o.Value).Returns(new TeamsOptions { DefaultSizeLimit = _defaultSizeLimit, DefaultTeamsCountLimit = 5 });
        _selfHostedOptions = new Mock<IOptions<SelfHostedOptions>>(MockBehavior.Strict);
        _selfHostedOptions.Setup(o => o.Value).Returns(new SelfHostedOptions { Enabled = true });
        _sut = new TeamsService(_teamsDbClient.Object, _teamMembersDbClient.Object, _userCacheInvalidator.Object, _options.Object, _selfHostedOptions.Object);
    }

    [Fact]
    public async Task AutocompleteTeamsAsync_ReturnsRepositoryResult()
    {
        // Arrange
        var workspaceId = 1;
        var search = "core";
        var skip = 0;
        var take = 10;
        var now = DateTimeOffset.UtcNow;
        var expected = new[]
        {
            new Team
            {
                Id = 7,
                WorkspaceId = workspaceId,
                Name = "Core Team",
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        _teamsDbClient
            .Setup(r => r.AutocompleteTeamsAsync(workspaceId, search, skip, take))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.AutocompleteTeamsAsync(workspaceId, search, skip, take);

        // Assert
        Assert.Single(result);
        Assert.Equal(expected[0].Id, result[0].Id);
        Assert.Equal(expected[0].Name, result[0].Name);
        _teamsDbClient.Verify(r => r.AutocompleteTeamsAsync(workspaceId, search, skip, take), Times.Once);
        _teamMembersDbClient.VerifyNoOtherCalls();
        _userCacheInvalidator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateTeamAsync_CreatesTeam_WhenUserHasTeam()
    {
        // Arrange
        var workspaceId = 1;
        var name = "Test Team";
        var userId = 42L;
        var userTeamId = 10;
        var now = DateTimeOffset.UtcNow;

        var expectedTeam = new Team
        {
            Id = 5,
            WorkspaceId = workspaceId,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now
        };

        _teamsDbClient
            .Setup(r => r.CreateTeamAsync(workspaceId, name))
            .ReturnsAsync(expectedTeam);

        // Act
        var result = await _sut.CreateTeamAsync(workspaceId, name, userId, userTeamId);

        // Assert
        Assert.Null(result.Error);
        Assert.Equal(expectedTeam.Id, result.Value!.Id);
        Assert.Equal(expectedTeam.Name, result.Value!.Name);
        Assert.Equal(expectedTeam.WorkspaceId, result.Value!.WorkspaceId);

        _teamsDbClient.Verify(r => r.CreateTeamAsync(workspaceId, name), Times.Once);
        _teamMembersDbClient.Verify(r => r.CreateTeamMemberAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateTeamAsync_CreatesTeamAndAddsUser_WhenUserHasNoTeam()
    {
        // Arrange
        var workspaceId = 1;
        var name = "Test Team";
        var userId = 42L;
        int? userTeamId = null;
        var now = DateTimeOffset.UtcNow;

        var expectedTeam = new Team
        {
            Id = 5,
            WorkspaceId = workspaceId,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now
        };

        _teamsDbClient
            .Setup(r => r.CreateTeamAsync(workspaceId, name))
            .ReturnsAsync(expectedTeam);

        _teamMembersDbClient
            .Setup(r => r.CreateTeamMemberAsync(userId, expectedTeam.Id, 10))
            .ReturnsAsync((new TeamMember
            {
                CreatedAt = now,
                UserId = userId,
                TeamId = expectedTeam.Id
            }, null));

        _userCacheInvalidator
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateTeamAsync(workspaceId, name, userId, userTeamId);

        // Assert
        Assert.Null(result.Error);
        Assert.Equal(expectedTeam.Id, result.Value!.Id);
        Assert.Equal(expectedTeam.Name, result.Value!.Name);
        Assert.Equal(expectedTeam.WorkspaceId, result.Value!.WorkspaceId);

        _teamsDbClient.Verify(r => r.CreateTeamAsync(workspaceId, name), Times.Once);
        _teamMembersDbClient.Verify(r => r.CreateTeamMemberAsync(userId, expectedTeam.Id, 10), Times.Once);
        _userCacheInvalidator.Verify(r => r.InvalidateUserCacheAsync(userId), Times.Once);
    }

    [Fact]
    public async Task CreateTeamAsync_AddsUserToCorrectTeam()
    {
        // Arrange
        var workspaceId = 1;
        var name = "Test Team";
        var userId = 100L;
        int? userTeamId = null;
        var now = DateTimeOffset.UtcNow;

        var createdTeam = new Team
        {
            Id = 999,
            WorkspaceId = workspaceId,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now
        };

        _teamsDbClient
            .Setup(r => r.CreateTeamAsync(workspaceId, name))
            .ReturnsAsync(createdTeam);

        _teamMembersDbClient
            .Setup(r => r.CreateTeamMemberAsync(userId, createdTeam.Id, 10))
            .ReturnsAsync((new TeamMember
            {
                CreatedAt = now,
                UserId = userId,
                TeamId = createdTeam.Id
            }, null))
            .Verifiable();

        _userCacheInvalidator
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CreateTeamAsync(workspaceId, name, userId, userTeamId);

        // Assert
        _teamMembersDbClient.Verify(r => r.CreateTeamMemberAsync(userId, createdTeam.Id, 10), Times.Once);
        _userCacheInvalidator.Verify(r => r.InvalidateUserCacheAsync(userId), Times.Once);
    }

    [Fact]
    public async Task UpdateTeamAsync_UpdatesTeam()
    {
        // Arrange
        var workspaceId = 1;
        var teamId = 7;
        var name = "Updated Team";
        var now = DateTimeOffset.UtcNow;

        var expectedTeam = new Team
        {
            Id = teamId,
            WorkspaceId = workspaceId,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now
        };

        _teamsDbClient
            .Setup(r => r.UpdateTeamAsync(workspaceId, teamId, name))
            .ReturnsAsync(expectedTeam);

        // Act
        var result = await _sut.UpdateTeamAsync(workspaceId, teamId, name);

        // Assert
        Assert.Equal(expectedTeam.Id, result.Id);
        Assert.Equal(expectedTeam.Name, result.Name);
        Assert.Equal(expectedTeam.WorkspaceId, result.WorkspaceId);

        _teamsDbClient.Verify(r => r.UpdateTeamAsync(workspaceId, teamId, name), Times.Once);
        _teamMembersDbClient.VerifyNoOtherCalls();
        _userCacheInvalidator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteTeamAsync_DeletesTeam()
    {
        // Arrange
        var workspaceId = 2;
        var teamId = 11;

        _teamsDbClient
            .Setup(r => r.DeleteTeamAsync(workspaceId, teamId))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteTeamAsync(workspaceId, teamId);

        // Assert
        _teamsDbClient.Verify(r => r.DeleteTeamAsync(workspaceId, teamId), Times.Once);
        _teamMembersDbClient.VerifyNoOtherCalls();
        _userCacheInvalidator.VerifyNoOtherCalls();
    }
}
