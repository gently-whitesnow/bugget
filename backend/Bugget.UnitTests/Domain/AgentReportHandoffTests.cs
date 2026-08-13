using Bugget.Domain.Reports;
using FluentAssertions;

namespace Bugget.UnitTests.Domain;

/// <summary>
/// Передача ответственности при смене статуса агентом (kaiten 238350):
/// Fix — репорт держит владелец PAT, Test — репорт возвращается тестировщику
/// (прежнему ответственному, или автору, если ответственного не было).
/// </summary>
public sealed class AgentReportHandoffTests
{
    private const string Owner = "pat-owner";
    private const string Tester = "tester";
    private const string Creator = "creator";

    [Fact]
    public void Fix_AssignsTokenOwner()
    {
        AgentReportHandoff.ResolveResponsible(
                ReportStatus.Fix, Owner, Tester, pastResponsibleUserId: null, Creator)
            .Should().Be(Owner);
    }

    [Fact]
    public void Fix_WhenOwnerAlreadyResponsible_NoChange()
    {
        AgentReportHandoff.ResolveResponsible(
                ReportStatus.Fix, Owner, Owner, pastResponsibleUserId: Tester, Creator)
            .Should().BeNull();
    }

    [Fact]
    public void Fix_WhenNobodyResponsible_AssignsTokenOwner()
    {
        AgentReportHandoff.ResolveResponsible(
                ReportStatus.Fix, Owner, responsibleUserId: null, pastResponsibleUserId: null, Creator)
            .Should().Be(Owner);
    }

    [Fact]
    public void Test_ReturnsReportToPastResponsible()
    {
        AgentReportHandoff.ResolveResponsible(
                ReportStatus.Test, Owner, responsibleUserId: Owner, pastResponsibleUserId: Tester, Creator)
            .Should().Be(Tester);
    }

    [Fact]
    public void Test_WithoutPastResponsible_FallsBackToCreator()
    {
        AgentReportHandoff.ResolveResponsible(
                ReportStatus.Test, Owner, responsibleUserId: Owner, pastResponsibleUserId: null, Creator)
            .Should().Be(Creator);
    }

    [Fact]
    public void Test_WhenAnotherHumanHoldsReport_NoChange()
    {
        // Репорт держит человек (не владелец токена) — отбирать нельзя.
        AgentReportHandoff.ResolveResponsible(
                ReportStatus.Test, Owner, responsibleUserId: Tester, pastResponsibleUserId: Creator, Creator)
            .Should().BeNull();
    }

    [Fact]
    public void Test_WhenNobodyResponsible_AssignsTester()
    {
        AgentReportHandoff.ResolveResponsible(
                ReportStatus.Test, Owner, responsibleUserId: null, pastResponsibleUserId: Tester, Creator)
            .Should().Be(Tester);
    }

    [Fact]
    public void Test_WhenTesterIsTokenOwner_NoChange()
    {
        // Владелец токена сам был тестировщиком: past == current, менять нечего.
        AgentReportHandoff.ResolveResponsible(
                ReportStatus.Test, Owner, responsibleUserId: Owner, pastResponsibleUserId: Owner, Creator)
            .Should().BeNull();
    }

    [Theory]
    [InlineData(ReportStatus.Backlog)]
    [InlineData(ReportStatus.Resolved)]
    [InlineData(ReportStatus.Rejected)]
    public void OtherStatuses_NeverTouchResponsible(ReportStatus status)
    {
        AgentReportHandoff.ResolveResponsible(
                status, Owner, Tester, pastResponsibleUserId: Creator, Creator)
            .Should().BeNull();
    }
}
