using Bugget.Api.Mappers;
using Bugget.Contracts.Reports.Generated;
using FluentAssertions;
using DomainModel = Bugget.Domain;

namespace Bugget.UnitTests.Mappers;

/// <summary>
/// Числа домена и БД ↔ enum'ы провода.
///
/// Диапазон проверяется целиком в обе стороны: соответствие задано поимённо, и
/// молчаливая перестановка одного значения — это чужой статус в карточке
/// заказчика, а не ошибка сборки. Отдельно фиксируется fail-closed: числа, для
/// которого в контракте нет значения, наружу не уходит вовсе.
/// </summary>
public sealed class WireEnumMapperTests
{
    [Theory(DisplayName = "Статус репорта: число домена ↔ значение контракта")]
    [InlineData(DomainModel.Reports.ReportStatus.Backlog, ReportStatus.Backlog)]
    [InlineData(DomainModel.Reports.ReportStatus.Resolved, ReportStatus.Resolved)]
    [InlineData(DomainModel.Reports.ReportStatus.Fix, ReportStatus.Fix)]
    [InlineData(DomainModel.Reports.ReportStatus.Rejected, ReportStatus.Rejected)]
    [InlineData(DomainModel.Reports.ReportStatus.Test, ReportStatus.Test)]
    public void ReportStatusRoundTrips(DomainModel.Reports.ReportStatus domain, ReportStatus wire)
    {
        WireEnumMapper.ToReportStatusWire((int)domain).Should().Be(wire);
        wire.ToDomainValue().Should().Be((int)domain);
    }

    [Theory(DisplayName = "Статус бага: число домена ↔ значение контракта")]
    [InlineData(DomainModel.Bugs.BugStatus.Open, BugStatus.Open)]
    [InlineData(DomainModel.Bugs.BugStatus.Verified, BugStatus.Verified)]
    [InlineData(DomainModel.Bugs.BugStatus.Rejected, BugStatus.Rejected)]
    [InlineData(DomainModel.Bugs.BugStatus.Fixed, BugStatus.Fixed)]
    public void BugStatusRoundTrips(DomainModel.Bugs.BugStatus domain, BugStatus wire)
    {
        WireEnumMapper.ToBugStatusWire((int)domain).Should().Be(wire);
        wire.ToDomainValue().Should().Be((int)domain);
    }

    [Theory(DisplayName = "Тип автора: число домена ↔ значение контракта")]
    [InlineData(DomainModel.Common.CreatorType.User, CreatorType.User)]
    [InlineData(DomainModel.Common.CreatorType.System, CreatorType.System)]
    [InlineData(DomainModel.Common.CreatorType.TgBetaTester, CreatorType.Tg_beta_tester)]
    [InlineData(DomainModel.Common.CreatorType.Agent, CreatorType.Agent)]
    public void CreatorTypeRoundTrips(DomainModel.Common.CreatorType domain, CreatorType wire)
    {
        WireEnumMapper.ToCreatorTypeWire((int)domain).Should().Be(wire);
        wire.ToDomainValue().Should().Be((int)domain);
    }

    [Theory(DisplayName = "Аудитория комментария: число домена ↔ значение контракта")]
    [InlineData(DomainModel.Common.CommentAudience.Internal, CommentAudience.Internal)]
    [InlineData(DomainModel.Common.CommentAudience.External, CommentAudience.External)]
    public void CommentAudienceRoundTrips(DomainModel.Common.CommentAudience domain, CommentAudience wire)
    {
        WireEnumMapper.ToCommentAudienceWire((int)domain).Should().Be(wire);
        wire.ToDomainValue().Should().Be((int)domain);
    }

    [Theory(DisplayName = "Тип вложения: число домена ↔ значение контракта")]
    [InlineData(DomainModel.AttachType.Fact, AttachType.Fact)]
    [InlineData(DomainModel.AttachType.Expected, AttachType.Expected)]
    [InlineData(DomainModel.AttachType.Comment, AttachType.Comment)]
    [InlineData(DomainModel.AttachType.BugStep, AttachType.Bug_step)]
    public void AttachTypeRoundTrips(DomainModel.AttachType domain, AttachType wire)
    {
        WireEnumMapper.ToAttachTypeWire((int)domain).Should().Be(wire);
        wire.ToDomain().Should().Be(domain);
    }

    [Theory(DisplayName = "Число вне контракта наружу не уходит")]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(99)]
    public void UnknownDomainValueFailsClosed(int value)
    {
        var toReportStatus = () => WireEnumMapper.ToReportStatusWire(value);
        var toBugStatus = () => WireEnumMapper.ToBugStatusWire(value);
        var toCreatorType = () => WireEnumMapper.ToCreatorTypeWire(value);
        var toAudience = () => WireEnumMapper.ToCommentAudienceWire(value);
        var toAttachType = () => WireEnumMapper.ToAttachTypeWire(value);

        toReportStatus.Should().Throw<InvalidOperationException>();
        toBugStatus.Should().Throw<InvalidOperationException>();
        toCreatorType.Should().Throw<InvalidOperationException>();
        toAudience.Should().Throw<InvalidOperationException>();
        toAttachType.Should().Throw<InvalidOperationException>();
    }
}
