using Bugget.Api.Modules.InProcess;
using Bugget.Application.Users.Interfaces;
using Bugget.Domain.Users;
using Moq;

namespace Bugget.UnitTests.Modules.InProcess;

/// <summary>
/// Сценарии, унаследованные от HTTP-клиента в users-api: маппинг long id -> string userId
/// и fallback для пользователей, которых модуль users не вернул.
/// </summary>
public class UsersClientAdapterTests
{
    private static (UsersClientAdapter adapter, Mock<IUsersService> usersService) CreateAdapter()
    {
        var usersService = new Mock<IUsersService>();
        return (new UsersClientAdapter(usersService.Object), usersService);
    }

    private static User User(long id, string name, string? imageUrl = null, string? mattermostUserId = null) => new()
    {
        Id = id,
        ExternalId = $"ext-{id}",
        Name = name,
        ImageUrl = imageUrl,
        MattermostUserId = mattermostUserId,
        RegistrationDate = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
    };

    [Fact(DisplayName = "Маппит ответ по строковому ключу: long Id из модуля users ↔ string userId на входе")]
    public async Task GetUsersAsync_MapsLongIdToStringUserId()
    {
        var (adapter, usersService) = CreateAdapter();
        usersService
            .Setup(s => s.ListUsersAsync(It.IsAny<long[]>(), null))
            .ReturnsAsync([
                User(42, "Alice", "http://img/a.png", "mm-alice"),
                User(100, "Bob"),
            ]);

        var result = (await adapter.GetUsersAsync(["42", "100"])).ToArray();

        Assert.Equal(2, result.Length);

        var alice = result.Single(u => u.Id == "42");
        Assert.Equal("Alice", alice.Name);
        Assert.Equal("http://img/a.png", alice.ImageUrl);
        Assert.Equal("mm-alice", alice.MattermostUserId);

        var bob = result.Single(u => u.Id == "100");
        Assert.Equal("Bob", bob.Name);
        Assert.Null(bob.ImageUrl);
        Assert.Null(bob.MattermostUserId);

        usersService.Verify(s => s.ListUsersAsync(new[] { 42L, 100L }, null), Times.Once);
    }

    [Fact(DisplayName = "Непарсимые userId не уходят в модуль users и отдаются как fallback {Id=userId, Name=userId}")]
    public async Task GetUsersAsync_SkipsUnparseableIdsAndReturnsFallback()
    {
        var (adapter, usersService) = CreateAdapter();
        usersService
            .Setup(s => s.ListUsersAsync(It.IsAny<long[]>(), null))
            .ReturnsAsync([User(42, "Alice")]);

        var result = (await adapter.GetUsersAsync(["abc", "42", "not-a-long"])).ToArray();

        Assert.Equal(3, result.Length);
        Assert.Equal("abc", result.Single(u => u.Id == "abc").Name);
        Assert.Equal("not-a-long", result.Single(u => u.Id == "not-a-long").Name);
        Assert.Equal("Alice", result.Single(u => u.Id == "42").Name);

        usersService.Verify(s => s.ListUsersAsync(new[] { 42L }, null), Times.Once);
    }

    [Fact(DisplayName = "Все userId непарсимые — обращения в модуль users нет, все возвращаются как fallback")]
    public async Task GetUsersAsync_AllUnparseable_SkipsUsersModule()
    {
        var (adapter, usersService) = CreateAdapter();

        var result = (await adapter.GetUsersAsync(["abc", "x", "y"])).ToArray();

        Assert.Equal(3, result.Length);
        Assert.All(result, u => Assert.Equal(u.Id, u.Name));
        usersService.Verify(s => s.ListUsersAsync(It.IsAny<long[]>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact(DisplayName = "Модуль users вернул меньше пользователей — отсутствующие отдаются как fallback")]
    public async Task GetUsersAsync_PartialResponse_FillsMissingWithFallback()
    {
        var (adapter, usersService) = CreateAdapter();
        usersService
            .Setup(s => s.ListUsersAsync(It.IsAny<long[]>(), null))
            .ReturnsAsync([User(42, "Alice")]);

        var result = (await adapter.GetUsersAsync(["42", "100"])).ToArray();

        Assert.Equal(2, result.Length);
        Assert.Equal("Alice", result.Single(u => u.Id == "42").Name);

        var missing = result.Single(u => u.Id == "100");
        Assert.Equal("100", missing.Name);
        Assert.Null(missing.ImageUrl);
        Assert.Null(missing.MattermostUserId);
    }

    [Fact(DisplayName = "GetUserAsync: пользователь не найден — fallback {Id=userId, Name=userId}")]
    public async Task GetUserAsync_NotFound_ReturnsFallback()
    {
        var (adapter, usersService) = CreateAdapter();
        usersService
            .Setup(s => s.ListUsersAsync(It.IsAny<long[]>(), null))
            .ReturnsAsync([]);

        var user = await adapter.GetUserAsync("42");

        Assert.Equal("42", user.Id);
        Assert.Equal("42", user.Name);
    }
}
