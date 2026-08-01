using Bugget.Contracts.Reports.Generated;
using DomainModel = Bugget.Domain;

namespace Bugget.Api.Mappers;

/// <summary>
/// Числовое представление домена ↔ enum провода модуля reports.
///
/// Схема БД и доменные модели остаются числовыми, строка живёт только на
/// HTTP-границе (ADR-0012). Соответствие задано поимённо, а не приведением
/// типа: порядок значений в контракте сегодня совпадает с числами домена, но
/// это совпадение, а не правило — молчаливый <c>(int)</c> сломался бы на первом
/// же значении, вставленном в середину списка.
///
/// Наружу неизвестное число не уходит: это расхождение хранилища с контрактом,
/// и «ближайшее» значение вместо него — тихая порча данных у клиента.
/// </summary>
internal static class WireEnumMapper
{
    public static ReportStatus ToReportStatusWire(int value) => value switch
    {
        (int)DomainModel.Reports.ReportStatus.Backlog => ReportStatus.Backlog,
        (int)DomainModel.Reports.ReportStatus.Resolved => ReportStatus.Resolved,
        (int)DomainModel.Reports.ReportStatus.Fix => ReportStatus.Fix,
        (int)DomainModel.Reports.ReportStatus.Rejected => ReportStatus.Rejected,
        (int)DomainModel.Reports.ReportStatus.Test => ReportStatus.Test,
        _ => throw Unknown(nameof(ReportStatus), value)
    };

    public static int ToDomainValue(this ReportStatus value) => value switch
    {
        ReportStatus.Backlog => (int)DomainModel.Reports.ReportStatus.Backlog,
        ReportStatus.Resolved => (int)DomainModel.Reports.ReportStatus.Resolved,
        ReportStatus.Fix => (int)DomainModel.Reports.ReportStatus.Fix,
        ReportStatus.Rejected => (int)DomainModel.Reports.ReportStatus.Rejected,
        ReportStatus.Test => (int)DomainModel.Reports.ReportStatus.Test,
        _ => throw Unknown(nameof(ReportStatus), value)
    };

    public static BugStatus ToBugStatusWire(int value) => value switch
    {
        (int)DomainModel.Bugs.BugStatus.Open => BugStatus.Open,
        (int)DomainModel.Bugs.BugStatus.Verified => BugStatus.Verified,
        (int)DomainModel.Bugs.BugStatus.Rejected => BugStatus.Rejected,
        (int)DomainModel.Bugs.BugStatus.Fixed => BugStatus.Fixed,
        _ => throw Unknown(nameof(BugStatus), value)
    };

    public static int ToDomainValue(this BugStatus value) => value switch
    {
        BugStatus.Open => (int)DomainModel.Bugs.BugStatus.Open,
        BugStatus.Verified => (int)DomainModel.Bugs.BugStatus.Verified,
        BugStatus.Rejected => (int)DomainModel.Bugs.BugStatus.Rejected,
        BugStatus.Fixed => (int)DomainModel.Bugs.BugStatus.Fixed,
        _ => throw Unknown(nameof(BugStatus), value)
    };

    public static CreatorType ToCreatorTypeWire(int value) => value switch
    {
        (int)DomainModel.Common.CreatorType.User => CreatorType.User,
        (int)DomainModel.Common.CreatorType.System => CreatorType.System,
        (int)DomainModel.Common.CreatorType.TgBetaTester => CreatorType.Tg_beta_tester,
        _ => throw Unknown(nameof(CreatorType), value)
    };

    public static int ToDomainValue(this CreatorType value) => value switch
    {
        CreatorType.User => (int)DomainModel.Common.CreatorType.User,
        CreatorType.System => (int)DomainModel.Common.CreatorType.System,
        CreatorType.Tg_beta_tester => (int)DomainModel.Common.CreatorType.TgBetaTester,
        _ => throw Unknown(nameof(CreatorType), value)
    };

    public static CommentAudience ToCommentAudienceWire(int value) => value switch
    {
        (int)DomainModel.Common.CommentAudience.Internal => CommentAudience.Internal,
        (int)DomainModel.Common.CommentAudience.External => CommentAudience.External,
        _ => throw Unknown(nameof(CommentAudience), value)
    };

    public static int ToDomainValue(this CommentAudience value) => value switch
    {
        CommentAudience.Internal => (int)DomainModel.Common.CommentAudience.Internal,
        CommentAudience.External => (int)DomainModel.Common.CommentAudience.External,
        _ => throw Unknown(nameof(CommentAudience), value)
    };

    public static AttachType ToAttachTypeWire(int value) => value switch
    {
        (int)DomainModel.AttachType.Fact => AttachType.Fact,
        (int)DomainModel.AttachType.Expected => AttachType.Expected,
        (int)DomainModel.AttachType.Comment => AttachType.Comment,
        (int)DomainModel.AttachType.BugStep => AttachType.Bug_step,
        _ => throw Unknown(nameof(AttachType), value)
    };

    public static DomainModel.AttachType ToDomain(this AttachType value) => value switch
    {
        AttachType.Fact => DomainModel.AttachType.Fact,
        AttachType.Expected => DomainModel.AttachType.Expected,
        AttachType.Comment => DomainModel.AttachType.Comment,
        AttachType.Bug_step => DomainModel.AttachType.BugStep,
        _ => throw Unknown(nameof(AttachType), value)
    };

    private static InvalidOperationException Unknown(string name, object value) =>
        new($"Значение {value} не описано в контракте {name}.");
}
