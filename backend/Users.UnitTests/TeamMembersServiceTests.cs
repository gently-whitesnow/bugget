using Bugget.Entities.Errors;
using Microsoft.Extensions.Options;
using Moq;
using Users.BO.TeamMembers;
using Users.DA.Interfaces;
using Users.DA.TeamMembers;
using Users.Entities.DbModels.Members;
using Users.Entities.Options;
using Xunit;

namespace Users.UnitTests;

public class TeamMembersServiceTests
{
    private readonly Mock<ITeamMembersRepository> _teamMembersRepo;
    private readonly Mock<IWorkspaceMembersRepository> _workspaceMembersRepo;
    private readonly Mock<IAuthorizationRepository> _authorizationRepo;
    private readonly Mock<IOptions<TeamsOptions>> _options;
    private readonly Mock<IOptions<SelfHostedOptions>> _selfHostedOptions;
    private readonly TeamMembersService _sut;
    private readonly int _defaultSizeLimit = 10;

    public TeamMembersServiceTests()
    {
        _teamMembersRepo = new Mock<ITeamMembersRepository>(MockBehavior.Strict);
        _workspaceMembersRepo = new Mock<IWorkspaceMembersRepository>(MockBehavior.Strict);
        _authorizationRepo = new Mock<IAuthorizationRepository>(MockBehavior.Strict);
        _options = new Mock<IOptions<TeamsOptions>>(MockBehavior.Strict);
        _selfHostedOptions = new Mock<IOptions<SelfHostedOptions>>(MockBehavior.Strict);
        _selfHostedOptions.Setup(o => o.Value).Returns(new SelfHostedOptions { Enabled = false });
        _options.Setup(o => o.Value).Returns(new TeamsOptions
        {
            DefaultSizeLimit = _defaultSizeLimit,
            DefaultTeamsCountLimit = 5,
        });
        _sut = new TeamMembersService(_teamMembersRepo.Object, _options.Object, _workspaceMembersRepo.Object, _authorizationRepo.Object, _selfHostedOptions.Object);
    }

    [Fact(DisplayName = "CreateTeamMemberAsync успешно создает участника команды")]
    public async Task CreateTeamMemberAsync_WhenSuccess_ShouldReturnTeamMember()
    {
        // Arrange
        var teamId = 10;
        var userId = 42L;
        var now = DateTimeOffset.UtcNow;

        var expectedMember = new TeamMemberDbModel
        {
            TeamId = teamId,
            UserId = userId,
            CreatedAt = now
        };

        _teamMembersRepo
            .Setup(r => r.CreateTeamMemberAsync(userId, teamId, _defaultSizeLimit))
            .ReturnsAsync((expectedMember, null));

        _authorizationRepo
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateTeamMemberAsync(teamId, userId);

        // Assert
        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal(teamId, result.Value.TeamId);
        Assert.Equal(userId, result.Value.UserId);

        _teamMembersRepo.Verify(r => r.CreateTeamMemberAsync(userId, teamId, _defaultSizeLimit), Times.Once);
        _authorizationRepo.Verify(r => r.InvalidateUserCacheAsync(userId), Times.Once);
    }

    [Fact(DisplayName = "CreateTeamMemberAsync возвращает ошибку при превышении лимита команды")]
    public async Task CreateTeamMemberAsync_WhenLimitExceeded_ShouldReturnError()
    {
        // Arrange
        var teamId = 10;
        var userId = 42L;

        _teamMembersRepo
            .Setup(r => r.CreateTeamMemberAsync(userId, teamId, _defaultSizeLimit))
            .ReturnsAsync((null, TeamMembersErrors.TeamLimitExceededError));

        // Act
        var result = await _sut.CreateTeamMemberAsync(teamId, userId);

        // Assert
        Assert.NotNull(result.Error);
        Assert.Equal(TeamMembersErrors.TeamLimitExceededError, result.Error);

        _teamMembersRepo.Verify(r => r.CreateTeamMemberAsync(userId, teamId, _defaultSizeLimit), Times.Once);
        _authorizationRepo.Verify(r => r.InvalidateUserCacheAsync(It.IsAny<long>()), Times.Never);
    }

    [Fact(DisplayName = "CreateTeamMemberAsync вызывает InvalidateUserCacheAsync только при успешном создании")]
    public async Task CreateTeamMemberAsync_WhenSuccess_ShouldInvalidateCache()
    {
        // Arrange
        var teamId = 5;
        var userId = 100L;
        var now = DateTimeOffset.UtcNow;

        var member = new TeamMemberDbModel
        {
            TeamId = teamId,
            UserId = userId,
            CreatedAt = now
        };

        _teamMembersRepo
            .Setup(r => r.CreateTeamMemberAsync(userId, teamId, _defaultSizeLimit))
            .ReturnsAsync((member, null));

        _authorizationRepo
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await _sut.CreateTeamMemberAsync(teamId, userId);

        // Assert
        _authorizationRepo.Verify(r => r.InvalidateUserCacheAsync(userId), Times.Once);
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

        var customSut = new TeamMembersService(_teamMembersRepo.Object, customOptions.Object, _workspaceMembersRepo.Object, _authorizationRepo.Object, _selfHostedOptions.Object);

        var member = new TeamMemberDbModel
        {
            TeamId = teamId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _teamMembersRepo
            .Setup(r => r.CreateTeamMemberAsync(userId, teamId, customLimit))
            .ReturnsAsync((member, null));

        _authorizationRepo
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask);

        // Act
        await customSut.CreateTeamMemberAsync(teamId, userId);

        // Assert
        _teamMembersRepo.Verify(r => r.CreateTeamMemberAsync(userId, teamId, customLimit), Times.Once);
    }

    [Fact(DisplayName = "ListTeamMembersAsync возвращает список участников команды")]
    public async Task ListTeamMembersAsync_ShouldReturnTeamMembers()
    {
        // Arrange
        var teamId = 10;
        var now = DateTimeOffset.UtcNow;

        var expectedMembers = new[]
        {
            new TeamMemberDbModel { TeamId = teamId, UserId = 1L, CreatedAt = now },
            new TeamMemberDbModel { TeamId = teamId, UserId = 2L, CreatedAt = now },
            new TeamMemberDbModel { TeamId = teamId, UserId = 3L, CreatedAt = now }
        };

        _teamMembersRepo
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

        _teamMembersRepo.Verify(r => r.ListTeamMembersAsync(teamId), Times.Once);
    }

    [Fact(DisplayName = "ListTeamMembersAsync возвращает пустой массив для команды без участников")]
    public async Task ListTeamMembersAsync_WhenNoMembers_ShouldReturnEmptyArray()
    {
        // Arrange
        var teamId = 10;

        _teamMembersRepo
            .Setup(r => r.ListTeamMembersAsync(teamId))
            .ReturnsAsync(Array.Empty<TeamMemberDbModel>());

        // Act
        var result = await _sut.ListTeamMembersAsync(teamId);

        // Assert
        Assert.Empty(result);

        _teamMembersRepo.Verify(r => r.ListTeamMembersAsync(teamId), Times.Once);
    }

    [Fact(DisplayName = "DeleteTeamMemberAsync удаляет участника из команды и воркспейса")]
    public async Task DeleteTeamMemberAsync_ShouldDeleteFromTeamAndWorkspace()
    {
        // Arrange
        var teamId = 10;
        var userId = 42L;

        _teamMembersRepo
            .Setup(r => r.DeleteTeamMemberAsync(userId, teamId))
            .Returns(Task.CompletedTask);

        _workspaceMembersRepo
            .Setup(r => r.DeleteWorkspaceMemberAsync(userId, teamId))
            .Returns(Task.CompletedTask);

        _authorizationRepo
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteTeamMemberAsync(userId, teamId);

        // Assert
        _teamMembersRepo.Verify(r => r.DeleteTeamMemberAsync(userId, teamId), Times.Once);
        _workspaceMembersRepo.Verify(r => r.DeleteWorkspaceMemberAsync(userId, teamId), Times.Once);
        _authorizationRepo.Verify(r => r.InvalidateUserCacheAsync(userId), Times.Once);
    }

    [Fact(DisplayName = "DeleteTeamMemberAsync инвалидирует кэш пользователя")]
    public async Task DeleteTeamMemberAsync_ShouldInvalidateUserCache()
    {
        // Arrange
        var teamId = 5;
        var userId = 100L;

        _teamMembersRepo
            .Setup(r => r.DeleteTeamMemberAsync(userId, teamId))
            .Returns(Task.CompletedTask);

        _workspaceMembersRepo
            .Setup(r => r.DeleteWorkspaceMemberAsync(userId, teamId))
            .Returns(Task.CompletedTask);

        _authorizationRepo
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await _sut.DeleteTeamMemberAsync(userId, teamId);

        // Assert
        _authorizationRepo.Verify(r => r.InvalidateUserCacheAsync(userId), Times.Once);
    }

    [Fact(DisplayName = "DeleteTeamMemberAsync вызывает все операции в правильном порядке")]
    public async Task DeleteTeamMemberAsync_ShouldCallOperationsInCorrectOrder()
    {
        // Arrange
        var teamId = 10;
        var userId = 42L;
        var callOrder = new List<string>();

        _teamMembersRepo
            .Setup(r => r.DeleteTeamMemberAsync(userId, teamId))
            .Returns(Task.CompletedTask)
            .Callback(() => callOrder.Add("DeleteTeamMember"));

        _workspaceMembersRepo
            .Setup(r => r.DeleteWorkspaceMemberAsync(userId, teamId))
            .Returns(Task.CompletedTask)
            .Callback(() => callOrder.Add("DeleteWorkspaceMember"));

        _authorizationRepo
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
