using Microsoft.Extensions.Options;
using Moq;
using Users.BO;
using Users.DA.Interfaces;
using Users.Entities.BO;
using Users.Entities.DbModels.Members;
using Users.Entities.DbModels.Teams;
using Users.Entities.DbModels.Workspaces;
using Users.Entities.Options;
using Xunit;

namespace Users.UnitTests;

public class WorkspacesServiceTests
{
    private readonly Mock<IWorkspacesRepository> _workspacesRepo;
    private readonly Mock<ITeamsRepository> _teamsRepo;
    private readonly Mock<IAuthorizationRepository> _authorizationRepo;
    private readonly Mock<IMembersRepository> _membersRepo;
    private readonly WorkspacesService _sut;
    private readonly Mock<IOptions<SelfHostedOptions>> _hostingOptions;
    public WorkspacesServiceTests()
    {
        _workspacesRepo = new Mock<IWorkspacesRepository>(MockBehavior.Strict);
        _teamsRepo = new Mock<ITeamsRepository>(MockBehavior.Strict);
        _authorizationRepo = new Mock<IAuthorizationRepository>(MockBehavior.Strict);
        _membersRepo = new Mock<IMembersRepository>(MockBehavior.Strict);
        _hostingOptions = new Mock<IOptions<SelfHostedOptions>>(MockBehavior.Strict);
        _hostingOptions.Setup(o => o.Value).Returns(new SelfHostedOptions { Enabled = false });
        _sut = new WorkspacesService(
            _workspacesRepo.Object,
            _teamsRepo.Object,
            _membersRepo.Object,
            _authorizationRepo.Object,
            _hostingOptions.Object
        );
    }

    [Fact]
    public async Task ListWorkspacesAsync_ReturnsEmpty_WhenNoWorkspaces()
    {
        var userId = 42L;

        _workspacesRepo
            .Setup(r => r.ListWorkspacesAsync(userId))
            .ReturnsAsync(Array.Empty<WorkspaceDbModel>());

        var result = await _sut.GetWorkspacesContextAsync(userId);

        Assert.Empty(result.Workspaces);

        _workspacesRepo.Verify(r => r.ListWorkspacesAsync(userId), Times.Once);
        _teamsRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ListWorkspacesAsync_MapsTeams()
    {
        var userId = 7L;
        var now = DateTimeOffset.UtcNow;

        var workspaces = new[]
        {
            new WorkspaceDbModel { Id = 1, Name = "WS-1", CreatedAt = now, UpdatedAt = now },
            new WorkspaceDbModel { Id = 2, Name = "WS-2", CreatedAt = now, UpdatedAt = now },
        };

        var teams = new[]
        {
            new TeamDbModel { Id = 10, WorkspaceId = 1, Name = "A", CreatedAt = now, UpdatedAt = now },
            new TeamDbModel { Id = 11, WorkspaceId = 1, Name = "B", CreatedAt = now, UpdatedAt = now },
            new TeamDbModel { Id = 20, WorkspaceId = 2, Name = "X", CreatedAt = now, UpdatedAt = now },
        };

        _workspacesRepo
            .Setup(r => r.ListWorkspacesAsync(userId))
            .ReturnsAsync(workspaces);

        _teamsRepo
            .Setup(r => r.ListTeamsAsync(It.Is<int[]>(ids => ids.SequenceEqual(new[] { 1, 2 }))))
            .ReturnsAsync(teams);

        _membersRepo
            .Setup(r => r.ListMembersAsync(userId))
            .ReturnsAsync((Array.Empty<WorkspaceMemberDbModel>(), Array.Empty<TeamMemberDbModel>()));

        var result = await _sut.GetWorkspacesContextAsync(userId);

        Assert.Equal(2, result.Workspaces.Length);

        var ws1 = result.Workspaces.Single(w => w.Id == 1);
        Assert.NotNull(ws1.Teams);
        Assert.Equal(2, ws1.Teams!.Length);
        Assert.NotNull(ws1.Teams);
        Assert.Equal(2, ws1.Teams!.Length);
        Assert.Equal(10, ws1.Teams![0].Id);
        Assert.Equal(11, ws1.Teams![1].Id);

        _workspacesRepo.VerifyAll();
        _teamsRepo.VerifyAll();
        _membersRepo.Verify(r => r.ListMembersAsync(userId), Times.Once);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_ShouldCallBothInterfaces()
    {
        // Arrange
        var userId = 123L;
        var workspaceName = "Test Workspace";
        var now = DateTimeOffset.UtcNow;
        var expectedWorkspace = new WorkspaceDbModel
        {
            Id = 456,
            Name = workspaceName,
            CreatedAt = now,
            UpdatedAt = now
        };

        _workspacesRepo
            .Setup(r => r.CreateWorkspaceAsync(userId, workspaceName))
            .ReturnsAsync(expectedWorkspace);

        _authorizationRepo
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateWorkspaceAsync(userId, workspaceName);

        // Assert
        Assert.Null(result.Error);
        Assert.Equal(expectedWorkspace, result.Value);

        _workspacesRepo.Verify(r => r.CreateWorkspaceAsync(userId, workspaceName), Times.Once);
        _authorizationRepo.Verify(r => r.InvalidateUserCacheAsync(userId), Times.Once);

        _teamsRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_UpdatesAndInvalidatesCache()
    {
        // Arrange
        var userId = 42L;
        var workspaceId = 1;
        var newName = "Renamed Workspace";
        var now = DateTimeOffset.UtcNow;

        var expectedWorkspace = new WorkspaceDbModel
        {
            Id = workspaceId,
            Name = newName,
            CreatedAt = now,
            UpdatedAt = now
        };

        _workspacesRepo
            .Setup(r => r.UpdateWorkspaceAsync(workspaceId, newName))
            .ReturnsAsync(expectedWorkspace);

        _authorizationRepo
            .Setup(r => r.InvalidateUserCacheAsync(userId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.UpdateWorkspaceAsync(userId, workspaceId, newName);

        // Assert
        Assert.Null(result.Error);
        Assert.Equal(newName, result.Value!.Name);

        _workspacesRepo.Verify(r => r.UpdateWorkspaceAsync(workspaceId, newName), Times.Once);
        _authorizationRepo.Verify(r => r.InvalidateUserCacheAsync(userId), Times.Once);
        _teamsRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_ReturnsSelfHostedError_WhenSelfHosted()
    {
        // Arrange
        var selfHostedOptions = new Mock<IOptions<SelfHostedOptions>>(MockBehavior.Strict);
        selfHostedOptions.Setup(o => o.Value).Returns(new SelfHostedOptions { Enabled = true });

        var sut = new WorkspacesService(
            _workspacesRepo.Object,
            _teamsRepo.Object,
            _membersRepo.Object,
            _authorizationRepo.Object,
            selfHostedOptions.Object
        );

        // Act
        var result = await sut.UpdateWorkspaceAsync(42L, 1, "New Name");

        // Assert
        Assert.NotNull(result.Error);

        _workspacesRepo.VerifyNoOtherCalls();
        _authorizationRepo.VerifyNoOtherCalls();
    }
}

