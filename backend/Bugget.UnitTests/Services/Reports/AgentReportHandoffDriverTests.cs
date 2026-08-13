using System.Security.Claims;
using Bugget.Application.Commands.Report;
using Bugget.Application.DomainEvents;
using Bugget.Application.Options;
using Bugget.Application.Ports;
using Bugget.Application.Services.Reports;
using Bugget.Domain.Authentication;
using Bugget.Domain.Reports;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Bugget.UnitTests.Services.Reports;

/// <summary>
/// Подстановка ответственного в effective-патч, когда статус меняет агент
/// (kaiten 238350). Драйвер проверяется через публичный
/// <see cref="ReportsService.PatchReportAsync"/>: тесты утверждают, какой DTO
/// в итоге уходит в БД.
/// </summary>
public sealed class AgentReportHandoffDriverTests
{
    private const int ReportId = 42;
    private const string Owner = "pat-owner";
    private const string Tester = "tester";
    private const string Creator = "creator";

    [Fact]
    public async Task AgentMovesToFix_ResponsibleBecomesTokenOwner()
    {
        var (svc, observed) = BuildSut(Snapshot(ReportStatus.Backlog, Tester, past: null));

        await svc.PatchReportAsync("42", Agent(Owner), new ReportPatchDto { Status = (int)ReportStatus.Fix });

        observed.Should().ContainSingle().Which.ResponsibleUserId.Should().Be(Owner);
    }

    [Fact]
    public async Task AgentMovesToTest_ReportReturnsToTester()
    {
        var (svc, observed) = BuildSut(Snapshot(ReportStatus.Fix, Owner, past: Tester));

        await svc.PatchReportAsync("42", Agent(Owner), new ReportPatchDto { Status = (int)ReportStatus.Test });

        observed.Should().ContainSingle().Which.ResponsibleUserId.Should().Be(Tester);
    }

    [Fact]
    public async Task AgentMovesToTest_WithoutPastResponsible_ReportGoesToCreator()
    {
        var (svc, observed) = BuildSut(Snapshot(ReportStatus.Fix, Owner, past: null));

        await svc.PatchReportAsync("42", Agent(Owner), new ReportPatchDto { Status = (int)ReportStatus.Test });

        observed.Should().ContainSingle().Which.ResponsibleUserId.Should().Be(Creator);
    }

    [Fact]
    public async Task HumanMovesToFix_ResponsibleUntouched()
    {
        var (svc, observed) = BuildSut(Snapshot(ReportStatus.Backlog, Tester, past: null));

        await svc.PatchReportAsync("42", Human("u1"), new ReportPatchDto { Status = (int)ReportStatus.Fix });

        observed.Should().ContainSingle().Which.ResponsibleUserId.Should().BeNull();
    }

    [Fact]
    public async Task AgentPassesExplicitResponsible_ExplicitWins()
    {
        var (svc, observed) = BuildSut(Snapshot(ReportStatus.Backlog, Tester, past: null));

        await svc.PatchReportAsync(
            "42",
            Agent(Owner),
            new ReportPatchDto { Status = (int)ReportStatus.Fix, ResponsibleUserId = "explicit" });

        observed.Should().ContainSingle().Which.ResponsibleUserId.Should().Be("explicit");
    }

    private static ReportPatchSnapshot Snapshot(ReportStatus status, string? responsible, string? past) =>
        new((int)status, responsible, past, Creator);

    private static UserIdentity Agent(string id) => CreateUser(id, authMethod: AuthMethods.Pat);

    private static UserIdentity Human(string id) => CreateUser(id, authMethod: null);

    private static UserIdentity CreateUser(string id, string? authMethod)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, id)], "test");
        identity.AddClaim(new Claim(AuthClaims.OrganizationId, "org-1"));
        if (authMethod != null)
        {
            identity.AddClaim(new Claim(AuthClaims.AuthMethod, authMethod));
        }

        return new UserIdentity(new ClaimsPrincipal(identity));
    }

    private static (ReportsService Svc, List<ReportPatchDto> ObservedDtos) BuildSut(ReportPatchSnapshot snapshot)
    {
        var db = new Mock<IReportsDbClient>();
        db.Setup(x => x.ResolveReportIdAsync(It.IsAny<string>(), It.IsAny<string>(), ReportId, It.IsAny<Guid?>(), It.IsAny<int?>()))
            .ReturnsAsync(new ResolvedReportId { Id = ReportId, CreatorTeamId = null });
        db.Setup(x => x.GetPatchSnapshotAsync(It.IsAny<ITransactionScope>(), ReportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var observed = new List<ReportPatchDto>();
        db.Setup(x => x.PatchReportAsync(ReportId, It.IsAny<ReportPatchDto>(), It.IsAny<ITransactionScope?>(), It.IsAny<CancellationToken>()))
            .Callback<int, ReportPatchDto, ITransactionScope?, CancellationToken>((_, dto, _, _) => observed.Add(dto))
            .ReturnsAsync((int _, ReportPatchDto dto, ITransactionScope? _, CancellationToken _) => new ReportPatchResult
            {
                Id = ReportId,
                PublicId = Guid.NewGuid(),
                Title = "t",
                Status = dto.Status ?? snapshot.Status,
                ResponsibleUserId = dto.ResponsibleUserId ?? snapshot.ResponsibleUserId ?? Creator,
                PastResponsibleUserId = snapshot.ResponsibleUserId ?? "past",
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatorTeamId = null,
            });

        var publisher = new Mock<IDomainEventPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<Bugget.Domain.DomainEvents.DomainEvent>(), It.IsAny<ITransactionScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);

        var scope = new Mock<ITransactionScope>().Object;
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<ITransactionScope, CancellationToken, Task<(ReportPatchResult, ReportPatchDto)>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<ITransactionScope, CancellationToken, Task<(ReportPatchResult, ReportPatchDto)>>, CancellationToken>(
                (action, ct) => action(scope, ct));

        var taskQueue = new Mock<ITaskQueue>();
        taskQueue.Setup(x => x.EnqueueAsync(It.IsAny<Func<Task>>())).Returns(ValueTask.CompletedTask);

        var svc = new ReportsService(
            db.Object,
            taskQueue.Object,
            reportEventsService: null!,
            publisher.Object,
            uow.Object,
            Options.Create(new ReportAliasOptions { AliasMode = ReportAliasMode.Default }));

        return (svc, observed);
    }
}
