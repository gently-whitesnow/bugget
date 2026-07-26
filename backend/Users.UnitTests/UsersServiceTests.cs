using Moq;
using TaskQueue;
using Users.BO;
using Users.BO.Interfaces;
using Users.DA.Interfaces;
using Xunit;

namespace Users.UnitTests;

public class UsersServiceTests
{
    private readonly Mock<IUsersRepository> _usersRepo;
    private readonly Mock<IMembersRepository> _membersRepo;
    private readonly Mock<ITeamsService> _teamsService;
    private readonly Mock<ITaskQueue> _taskQueue;
    private readonly Mock<IAvatarDownloadService> _avatarService;
    private readonly UsersService _sut;

    public UsersServiceTests()
    {
        _usersRepo = new Mock<IUsersRepository>(MockBehavior.Strict);
        _membersRepo = new Mock<IMembersRepository>(MockBehavior.Strict);
        _teamsService = new Mock<ITeamsService>(MockBehavior.Strict);
        _taskQueue = new Mock<ITaskQueue>(MockBehavior.Loose);
        _avatarService = new Mock<IAvatarDownloadService>(MockBehavior.Strict);
        _sut = new UsersService(_usersRepo.Object, _membersRepo.Object, _teamsService.Object, _taskQueue.Object, _avatarService.Object);
    }

    [Fact]
    public async Task IsAdminAsync_ReturnsRepositoryValue()
    {
        const long userId = 42L;

        _usersRepo
            .Setup(r => r.IsAdminAsync(userId))
            .ReturnsAsync(true);

        var result = await _sut.IsAdminAsync(userId);

        Assert.True(result);
        _usersRepo.Verify(r => r.IsAdminAsync(userId), Times.Once);
    }
}
