using Bugget.Application.Ports;
using Bugget.Application.Users;
using Bugget.Application.Users.Interfaces;
using Bugget.Application.Users.Ports;
using Moq;
using Xunit;

namespace Bugget.UnitTests.Users;

public class UsersServiceTests
{
    private readonly Mock<IUsersDbClient> _usersDbClient;
    private readonly Mock<IMembersDbClient> _membersDbClient;
    private readonly Mock<ITeamsService> _teamsService;
    private readonly Mock<ITaskQueue> _taskQueue;
    private readonly Mock<IAvatarDownloadService> _avatarService;
    private readonly UsersService _sut;

    public UsersServiceTests()
    {
        _usersDbClient = new Mock<IUsersDbClient>(MockBehavior.Strict);
        _membersDbClient = new Mock<IMembersDbClient>(MockBehavior.Strict);
        _teamsService = new Mock<ITeamsService>(MockBehavior.Strict);
        _taskQueue = new Mock<ITaskQueue>(MockBehavior.Loose);
        _avatarService = new Mock<IAvatarDownloadService>(MockBehavior.Strict);
        _sut = new UsersService(_usersDbClient.Object, _membersDbClient.Object, _teamsService.Object, _taskQueue.Object, _avatarService.Object);
    }
}
