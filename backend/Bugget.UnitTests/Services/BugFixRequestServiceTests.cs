using System.Security.Claims;
using Bugget.Application.Ports;
using Bugget.Application.Services.Bugs;
using Bugget.Application.Services.Reports;
using Bugget.Domain.Authentication;
using Bugget.Domain.Bugs;
using Bugget.Domain.Comments;
using Bugget.Domain.Reports;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Bugget.UnitTests.Services;

/// <summary>
/// Кулдаун fix-request: двойной клик не спамит, время двигает FakeTimeProvider —
/// контракт «повтор не спамит бесконтрольно» проверяется детерминированно.
/// </summary>
public sealed class BugFixRequestServiceTests
{
    private readonly Mock<IBugFixRequestedNotifier> _notifier = new();
    private readonly Mock<ICommentsDbClient> _comments = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-08-07T12:00:00Z"));
    private readonly BugFixRequestService _service;

    public BugFixRequestServiceTests()
    {
        var reports = new Mock<IReportsService>();
        reports
            .Setup(r => r.ResolveReportByAliasAsync(It.IsAny<string>(), It.IsAny<UserIdentity>()))
            .ReturnsAsync(new ResolvedReportId { Id = 7, CreatorTeamId = "1" });

        var bugs = new Mock<IBugsService>();
        bugs
            .Setup(b => b.GetBugAsync(7, 42))
            .ReturnsAsync(new BugSummary
            {
                Id = 42,
                CreatorUserId = "tester",
                Status = 0,
                CreatorType = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

        var users = new Mock<IUsersClient>();
        users
            .Setup(u => u.GetUserAsync(It.IsAny<string>()))
            .ReturnsAsync(new Bugget.Domain.User { Id = "tester", Name = "Тестировщик" });

        // Лог-сервис конкретный (внутренний коллаборатор Application) — собирается
        // из мок-портов, счётчик создания комментариев остаётся тем же _comments.
        var commentLogs = new Bugget.Application.Services.Comments.CommentLogsService(
            users.Object, _comments.Object, Mock.Of<IReportPageHubClient>());

        _comments
            .Setup(c => c.CreateCommentAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(Comment());

        var taskQueue = new Mock<ITaskQueue>();
        taskQueue
            .Setup(q => q.EnqueueAsync(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>()))
            .Returns(ValueTask.CompletedTask);

        _service = new BugFixRequestService(
            reports.Object,
            bugs.Object,
            commentLogs,
            _notifier.Object,
            taskQueue.Object,
            _time);
    }

    [Fact]
    public async Task RepeatWithinCooldownCreatesNothing()
    {
        Assert.Null(await _service.RequestFixAsync(Identity(), "7", 42));
        Assert.Null(await _service.RequestFixAsync(Identity(), "7", 42));

        _comments.Verify(
            c => c.CreateCommentAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task FailedRequestDoesNotBlockRetry()
    {
        // Первый вызов падает на создании маркера: пользователь получит 500 и
        // нажмёт ещё раз. Если кулдаун остался занят, повтор ответит «принято»,
        // не сделав ничего, — отказ станет невидимым.
        _comments
            .SetupSequence(c => c.CreateCommentAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("база недоступна"))
            .ReturnsAsync(Comment());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RequestFixAsync(Identity(), "7", 42));

        Assert.Null(await _service.RequestFixAsync(Identity(), "7", 42));

        // Повтор реально сделал работу: маркер создан со второй попытки. Очередь
        // здесь замокана и делегат не выполняет, поэтому спрашиваем с того шага,
        // который сервис делает сам.
        _comments.Verify(
            c => c.CreateCommentAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task CooldownExpiresWithTime()
    {
        Assert.Null(await _service.RequestFixAsync(Identity(), "7", 42));
        _time.Advance(TimeSpan.FromMinutes(2));
        Assert.Null(await _service.RequestFixAsync(Identity(), "7", 42));

        _comments.Verify(
            c => c.CreateCommentAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Exactly(2));
    }

    private static CommentSummary Comment() => new()
    {
        Id = 1,
        BugId = 42,
        Text = "маркер",
        CreatorUserId = SystemCreators.Bot,
        CreatorType = 1,
        Audience = 0,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static UserIdentity Identity() =>
        new(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "tester"),
                new Claim(AuthClaims.TeamId, "1"),
                new Claim(AuthClaims.OrganizationId, "1"),
            ],
            "test")));
}
