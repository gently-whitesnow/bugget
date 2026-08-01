using Bugget.Application.Users;
using Bugget.Application.Users.Options;
using Bugget.Application.Users.Ports;
using Bugget.Domain.Users;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Bugget.UnitTests.Users;

public class WorkspacesServiceTests
{
    private readonly Mock<IWorkspacesDbClient> _workspacesDbClient;
    private readonly Mock<ITeamsDbClient> _teamsDbClient;
    private readonly Mock<IUserCacheInvalidator> _userCacheInvalidator;
    private readonly Mock<IMembersDbClient> _membersDbClient;
    private readonly WorkspacesService _sut;
    private readonly Mock<IOptions<SelfHostedOptions>> _hostingOptions;
    public WorkspacesServiceTests()
    {
        _workspacesDbClient = new Mock<IWorkspacesDbClient>(MockBehavior.Strict);
        _teamsDbClient = new Mock<ITeamsDbClient>(MockBehavior.Strict);
        _userCacheInvalidator = new Mock<IUserCacheInvalidator>(MockBehavior.Strict);
        _membersDbClient = new Mock<IMembersDbClient>(MockBehavior.Strict);
        _hostingOptions = new Mock<IOptions<SelfHostedOptions>>(MockBehavior.Strict);
        _hostingOptions.Setup(o => o.Value).Returns(new SelfHostedOptions { Enabled = false });
        _sut = new WorkspacesService(
            _workspacesDbClient.Object,
            _teamsDbClient.Object,
            _membersDbClient.Object,
            _userCacheInvalidator.Object,
            _hostingOptions.Object
        );
    }

    [Fact]
    public async Task ListWorkspacesAsync_ReturnsEmpty_WhenNoWorkspaces()
    {
        var userId = 42L;

        _workspacesDbClient
            .Setup(r => r.ListWorkspacesAsync(userId))
            .ReturnsAsync(Array.Empty<Workspace>());

        var result = await _sut.GetWorkspacesContextAsync(userId);

        Assert.Empty(result.Workspaces);

        _workspacesDbClient.Verify(r => r.ListWorkspacesAsync(userId), Times.Once);
        _teamsDbClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ListWorkspacesAsync_MapsTeams()
    {
        var userId = 7L;
        var now = DateTimeOffset.UtcNow;

        var workspaces = new[]
        {
            new Workspace { Id = 1, Name = "WS-1", CreatedAt = now, UpdatedAt = now },
            new Workspace { Id = 2, Name = "WS-2", CreatedAt = now, UpdatedAt = now },
        };

        var teams = new[]
        {
            new Team { Id = 10, WorkspaceId = 1, Name = "A", CreatedAt = now, UpdatedAt = now },
            new Team { Id = 11, WorkspaceId = 1, Name = "B", CreatedAt = now, UpdatedAt = now },
            new Team { Id = 20, WorkspaceId = 2, Name = "X", CreatedAt = now, UpdatedAt = now },
        };

        _workspacesDbClient
            .Setup(r => r.ListWorkspacesAsync(userId))
            .ReturnsAsync(workspaces);

        _teamsDbClient
            .Setup(r => r.ListTeamsAsync(It.Is<int[]>(ids => ids.SequenceEqual(new[] { 1, 2 }))))
            .ReturnsAsync(teams);

        _membersDbClient
            .Setup(r => r.ListMembersAsync(userId))
            .ReturnsAsync((Array.Empty<WorkspaceMember>(), Array.Empty<TeamMember>()));

        var result = await _sut.GetWorkspacesContextAsync(userId);

        Assert.Equal(2, result.Workspaces.Length);

        var ws1 = result.Workspaces.Single(w => w.Id == 1);
        Assert.NotNull(ws1.Teams);
        Assert.Equal(2, ws1.Teams!.Length);
        Assert.NotNull(ws1.Teams);
        Assert.Equal(2, ws1.Teams!.Length);
        Assert.Equal(10, ws1.Teams![0].Id);
        Assert.Equal(11, ws1.Teams![1].Id);

        _workspacesDbClient.VerifyAll();
        _teamsDbClient.VerifyAll();
        _membersDbClient.Verify(r => r.ListMembersAsync(userId), Times.Once);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_ShouldCallBothInterfaces()
    {
        // Arrange
        var userId = 123L;
        var workspaceName = "Test Workspace";
        var now = DateTimeOffset.UtcNow;
        var expectedWorkspace = new Workspace
        {
            Id = 456,
            Name = workspaceName,
            CreatedAt = now,
            UpdatedAt = now
        };

        _workspacesDbClient
            .Setup(r => r.CreateWorkspaceAsync(userId, workspaceName))
            .ReturnsAsync(expectedWorkspace);

        _userCacheInvalidator
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateWorkspaceAsync(userId, workspaceName);

        // Assert
        Assert.Null(result.Error);
        Assert.Equal(expectedWorkspace, result.Value);

        _workspacesDbClient.Verify(r => r.CreateWorkspaceAsync(userId, workspaceName), Times.Once);
        _userCacheInvalidator.Verify(r => r.InvalidateUserCacheAsync(userId), Times.Once);

        _teamsDbClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_UpdatesAndInvalidatesCache()
    {
        // Arrange
        var userId = 42L;
        var workspaceId = 1;
        var newName = "Renamed Workspace";
        var now = DateTimeOffset.UtcNow;

        var expectedWorkspace = new Workspace
        {
            Id = workspaceId,
            Name = newName,
            CreatedAt = now,
            UpdatedAt = now
        };

        _workspacesDbClient
            .Setup(r => r.UpdateWorkspaceAsync(workspaceId, newName))
            .ReturnsAsync(expectedWorkspace);

        _userCacheInvalidator
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.UpdateWorkspaceAsync(userId, workspaceId, newName);

        // Assert
        Assert.Null(result.Error);
        Assert.Equal(newName, result.Value!.Name);

        _workspacesDbClient.Verify(r => r.UpdateWorkspaceAsync(workspaceId, newName), Times.Once);
        _userCacheInvalidator.Verify(r => r.InvalidateUserCacheAsync(userId), Times.Once);
        _teamsDbClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_ReturnsSelfHostedError_WhenSelfHosted()
    {
        // Arrange
        var selfHostedOptions = new Mock<IOptions<SelfHostedOptions>>(MockBehavior.Strict);
        selfHostedOptions.Setup(o => o.Value).Returns(new SelfHostedOptions { Enabled = true });

        var sut = new WorkspacesService(
            _workspacesDbClient.Object,
            _teamsDbClient.Object,
            _membersDbClient.Object,
            _userCacheInvalidator.Object,
            selfHostedOptions.Object
        );

        // Act
        var result = await sut.UpdateWorkspaceAsync(42L, 1, "New Name");

        // Assert
        Assert.NotNull(result.Error);

        _workspacesDbClient.VerifyNoOtherCalls();
        _userCacheInvalidator.VerifyNoOtherCalls();
    }
}

