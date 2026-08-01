using Bugget.Application.Users.Options;
using Bugget.Application.Users.Ports;
using Bugget.Application.Users.TeamMembers;
using Bugget.Domain.Errors;
using Bugget.Domain.Users;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Bugget.UnitTests.Users;

public class TeamMembersServiceTests
{
    private readonly Mock<ITeamMembersDbClient> _teamMembersDbClient;
    private readonly Mock<IWorkspaceMembersDbClient> _workspaceMembersDbClient;
    private readonly Mock<IUserCacheInvalidator> _userCacheInvalidator;
    private readonly Mock<IOptions<TeamsOptions>> _options;
    private readonly Mock<IOptions<SelfHostedOptions>> _selfHostedOptions;
    private readonly TeamMembersService _sut;
    private readonly int _defaultSizeLimit = 10;

    public TeamMembersServiceTests()
    {
        _teamMembersDbClient = new Mock<ITeamMembersDbClient>(MockBehavior.Strict);
        _workspaceMembersDbClient = new Mock<IWorkspaceMembersDbClient>(MockBehavior.Strict);
        _userCacheInvalidator = new Mock<IUserCacheInvalidator>(MockBehavior.Strict);
        _options = new Mock<IOptions<TeamsOptions>>(MockBehavior.Strict);
        _selfHostedOptions = new Mock<IOptions<SelfHostedOptions>>(MockBehavior.Strict);
        _selfHostedOptions.Setup(o => o.Value).Returns(new SelfHostedOptions { Enabled = false });
        _options.Setup(o => o.Value).Returns(new TeamsOptions
        {
            DefaultSizeLimit = _defaultSizeLimit,
            DefaultTeamsCountLimit = 5,
        });
        _sut = new TeamMembersService(_teamMembersDbClient.Object, _options.Object, _workspaceMembersDbClient.Object, _userCacheInvalidator.Object, _selfHostedOptions.Object);
    }

    [Fact(DisplayName = "CreateTeamMemberAsync успешно создает участника команды")]
    public async Task CreateTeamMemberAsync_WhenSuccess_ShouldReturnTeamMember()
    {
        // Arrange
        var teamId = 10;
        var userId = 42L;
        var now = DateTimeOffset.UtcNow;

        var expectedMember = new TeamMember
        {
            TeamId = teamId,
            UserId = userId,
            CreatedAt = now
        };

        _teamMembersDbClient
            .Setup(r => r.CreateTeamMemberAsync(userId, teamId, _defaultSizeLimit))
            .ReturnsAsync((expectedMember, null));

        _userCacheInvalidator
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateTeamMemberAsync(teamId, userId);

        // Assert
        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal(teamId, result.Value.TeamId);
        Assert.Equal(userId, result.Value.UserId);

        _teamMembersDbClient.Verify(r => r.CreateTeamMemberAsync(userId, teamId, _defaultSizeLimit), Times.Once);
        _userCacheInvalidator.Verify(r => r.InvalidateUserCacheAsync(userId), Times.Once);
    }

    [Fact(DisplayName = "CreateTeamMemberAsync возвращает ошибку при превышении лимита команды")]
    public async Task CreateTeamMemberAsync_WhenLimitExceeded_ShouldReturnError()
    {
        // Arrange
        var teamId = 10;
        var userId = 42L;

        _teamMembersDbClient
            .Setup(r => r.CreateTeamMemberAsync(userId, teamId, _defaultSizeLimit))
            .ReturnsAsync((null, TeamMembersErrors.TeamLimitExceededError));

        // Act
        var result = await _sut.CreateTeamMemberAsync(teamId, userId);

        // Assert
        Assert.NotNull(result.Error);
        Assert.Equal(TeamMembersErrors.TeamLimitExceededError, result.Error);

        _teamMembersDbClient.Verify(r => r.CreateTeamMemberAsync(userId, teamId, _defaultSizeLimit), Times.Once);
        _userCacheInvalidator.Verify(r => r.InvalidateUserCacheAsync(It.IsAny<long>()), Times.Never);
    }

    [Fact(DisplayName = "CreateTeamMemberAsync вызывает InvalidateUserCacheAsync только при успешном создании")]
    public async Task CreateTeamMemberAsync_WhenSuccess_ShouldInvalidateCache()
    {
        // Arrange
        var teamId = 5;
        var userId = 100L;
        var now = DateTimeOffset.UtcNow;

        var member = new TeamMember
        {
            TeamId = teamId,
            UserId = userId,
            CreatedAt = now
        };

        _teamMembersDbClient
            .Setup(r => r.CreateTeamMemberAsync(userId, teamId, _defaultSizeLimit))
            .ReturnsAsync((member, null));

        _userCacheInvalidator
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await _sut.CreateTeamMemberAsync(teamId, userId);

        // Assert
        _userCacheInvalidator.Verify(r => r.InvalidateUserCacheAsync(userId), Times.Once);
    }

    [Fact(DisplayName = "CreateTeamMemberAsync использует DefaultSizeLimit из конфигурации")]
    public async Task CreateTeamMemberAsync_ShouldUseDefaultSizeLimitFromOptions()
    {
        // Arrange
        var teamId = 10;
        var userId = 42L;
        var customLimit = 25;

        var customOptions = new Mock<IOptions<TeamsOptions>>(MockBehavior.Strict);
        customOptions.Setup(o => o.Value).Returns(new TeamsOptions
        {
            DefaultSizeLimit = customLimit,
            DefaultTeamsCountLimit = 5,
        });

        var customSut = new TeamMembersService(_teamMembersDbClient.Object, customOptions.Object, _workspaceMembersDbClient.Object, _userCacheInvalidator.Object, _selfHostedOptions.Object);

        var member = new TeamMember
        {
            TeamId = teamId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _teamMembersDbClient
            .Setup(r => r.CreateTeamMemberAsync(userId, teamId, customLimit))
            .ReturnsAsync((member, null));

        _userCacheInvalidator
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask);

        // Act
        await customSut.CreateTeamMemberAsync(teamId, userId);

        // Assert
        _teamMembersDbClient.Verify(r => r.CreateTeamMemberAsync(userId, teamId, customLimit), Times.Once);
    }

    [Fact(DisplayName = "ListTeamMembersAsync возвращает список участников команды")]
    public async Task ListTeamMembersAsync_ShouldReturnTeamMembers()
    {
        // Arrange
        var teamId = 10;
        var now = DateTimeOffset.UtcNow;

        var expectedMembers = new[]
        {
            new TeamMember { TeamId = teamId, UserId = 1L, CreatedAt = now },
            new TeamMember { TeamId = teamId, UserId = 2L, CreatedAt = now },
            new TeamMember { TeamId = teamId, UserId = 3L, CreatedAt = now }
        };

        _teamMembersDbClient
            .Setup(r => r.ListTeamMembersAsync(teamId))
            .ReturnsAsync(expectedMembers);

        // Act
        var result = await _sut.ListTeamMembersAsync(teamId);

        // Assert
        Assert.Equal(3, result.Length);
        Assert.All(result, member => Assert.Equal(teamId, member.TeamId));
        Assert.Contains(result, m => m.UserId == 1L);
        Assert.Contains(result, m => m.UserId == 2L);
        Assert.Contains(result, m => m.UserId == 3L);

        _teamMembersDbClient.Verify(r => r.ListTeamMembersAsync(teamId), Times.Once);
    }

    [Fact(DisplayName = "ListTeamMembersAsync возвращает пустой массив для команды без участников")]
    public async Task ListTeamMembersAsync_WhenNoMembers_ShouldReturnEmptyArray()
    {
        // Arrange
        var teamId = 10;

        _teamMembersDbClient
            .Setup(r => r.ListTeamMembersAsync(teamId))
            .ReturnsAsync(Array.Empty<TeamMember>());

        // Act
        var result = await _sut.ListTeamMembersAsync(teamId);

        // Assert
        Assert.Empty(result);

        _teamMembersDbClient.Verify(r => r.ListTeamMembersAsync(teamId), Times.Once);
    }

    [Fact(DisplayName = "DeleteTeamMemberAsync удаляет участника из команды и воркспейса")]
    public async Task DeleteTeamMemberAsync_ShouldDeleteFromTeamAndWorkspace()
    {
        // Arrange
        var teamId = 10;
        var userId = 42L;

        _teamMembersDbClient
            .Setup(r => r.DeleteTeamMemberAsync(userId, teamId))
            .Returns(Task.CompletedTask);

        _workspaceMembersDbClient
            .Setup(r => r.DeleteWorkspaceMemberAsync(userId, teamId))
            .Returns(Task.CompletedTask);

        _userCacheInvalidator
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteTeamMemberAsync(userId, teamId);

        // Assert
        _teamMembersDbClient.Verify(r => r.DeleteTeamMemberAsync(userId, teamId), Times.Once);
        _workspaceMembersDbClient.Verify(r => r.DeleteWorkspaceMemberAsync(userId, teamId), Times.Once);
        _userCacheInvalidator.Verify(r => r.InvalidateUserCacheAsync(userId), Times.Once);
    }

    [Fact(DisplayName = "DeleteTeamMemberAsync инвалидирует кэш пользователя")]
    public async Task DeleteTeamMemberAsync_ShouldInvalidateUserCache()
    {
        // Arrange
        var teamId = 5;
        var userId = 100L;

        _teamMembersDbClient
            .Setup(r => r.DeleteTeamMemberAsync(userId, teamId))
            .Returns(Task.CompletedTask);

        _workspaceMembersDbClient
            .Setup(r => r.DeleteWorkspaceMemberAsync(userId, teamId))
            .Returns(Task.CompletedTask);

        _userCacheInvalidator
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await _sut.DeleteTeamMemberAsync(userId, teamId);

        // Assert
        _userCacheInvalidator.Verify(r => r.InvalidateUserCacheAsync(userId), Times.Once);
    }

    [Fact(DisplayName = "DeleteTeamMemberAsync вызывает все операции в правильном порядке")]
    public async Task DeleteTeamMemberAsync_ShouldCallOperationsInCorrectOrder()
    {
        // Arrange
        var teamId = 10;
        var userId = 42L;
        var callOrder = new List<string>();

        _teamMembersDbClient
            .Setup(r => r.DeleteTeamMemberAsync(userId, teamId))
            .Returns(Task.CompletedTask)
            .Callback(() => callOrder.Add("DeleteTeamMember"));

        _workspaceMembersDbClient
            .Setup(r => r.DeleteWorkspaceMemberAsync(userId, teamId))
            .Returns(Task.CompletedTask)
            .Callback(() => callOrder.Add("DeleteWorkspaceMember"));

        _userCacheInvalidator
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask)
            .Callback(() => callOrder.Add("InvalidateCache"));

        // Act
        await _sut.DeleteTeamMemberAsync(userId, teamId);

        // Assert
        Assert.Equal(3, callOrder.Count);
        Assert.Equal("DeleteTeamMember", callOrder[0]);
        Assert.Equal("DeleteWorkspaceMember", callOrder[1]);
        Assert.Equal("InvalidateCache", callOrder[2]);
    }
}
