using Bugget.Entities.BO.AttachmentBo;
using Bugget.Entities.BO.Bugs;
using Bugget.Entities.BO.Comments;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.Views.Reports;
using Bugget.Mappers;
using FluentAssertions;

namespace Bugget.Tests.Mappers;

/// <summary>
/// Форма ответов зафиксирована contract-снимками, но снимок предъявляет только то,
/// что оказалось в сиде: «коллекция не запрашивалась» и «коллекция пустая» через HTTP
/// не различить, а `null` в LIST теперь и вовсе недостижим. Здесь проверяется само
/// правило перекладки — обе ветки сразу.
/// </summary>
public class ReportsMapperTests
{
    private static readonly DateTimeOffset Moment = DateTimeOffset.UnixEpoch;

    [Fact(DisplayName = "LIST: элемент списка отдаёт баги и комментарии без вложений и шагов")]
    public void List_item_keeps_bugs_and_comments_only()
    {
        var view = Report(Bug(attachments: [Attachment()], comments: [Comment()]));

        var listItem = view.ToListContract();

        // Отсутствие `links`, `attachments` и `steps` держит не проверка, а типы:
        // у ReportListItem/BugListItem таких свойств нет, вернуть их нечем.
        listItem.Id.Should().Be(view.Id);
        var bug = listItem.Bugs.Should().ContainSingle().Which;
        bug.Comments.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact(DisplayName = "LIST: не запрошенные баги и комментарии остаются null, а не пустым списком")]
    public void List_item_keeps_null_for_not_requested_collections()
    {
        var view = Report();
        view.Bugs = null;

        var listItem = view.ToListContract();

        listItem.Bugs.Should().BeNull();

        var bugWithoutComments = Bug(attachments: null, comments: null).ToListContract();
        bugWithoutComments.Comments.Should().BeNull();
    }

    [Fact(DisplayName = "Вложение внутри репорта отдаётся публичной формой, без полей хранилища")]
    public void Attachment_is_mapped_to_public_summary()
    {
        var summary = Attachment().ToSummaryContract();

        summary.Id.Should().Be(10);
        summary.Entity_id.Should().Be(1);
        summary.File_name.Should().Be("shot.png");
        summary.Has_preview.Should().BeTrue();
    }

    /// <summary>
    /// `has_preview` в публичной форме не nullable: в базе колонка допускает `NULL`,
    /// наружу это уходит как «превью нет».
    /// </summary>
    [Fact(DisplayName = "Вложение без признака превью отдаёт has_preview = false")]
    public void Attachment_without_preview_flag_is_false()
    {
        var summary = Attachment(hasPreview: null).ToSummaryContract();

        summary.Has_preview.Should().BeFalse();
    }

    private static ReportViewModel Report(params Bug[]? bugs) => new()
    {
        Id = "42-1",
        Title = "репорт",
        Status = 0,
        ResponsibleUserId = "user-1",
        PastResponsibleUserId = "user-0",
        CreatorUserId = "user-1",
        CreatorTeamId = "1",
        CreatedAt = Moment,
        UpdatedAt = Moment,
        CreatorType = 0,
        IsExcludedFromAnalytics = false,
        ParticipantsUserIds = ["user-1"],
        Links = [new ReportLink
        {
            Id = 1,
            ReportId = 1,
            Link = "https://example.test",
            Name = "ссылка",
            CreatedAt = Moment,
            UpdatedAt = Moment,
        }],
        Bugs = bugs,
    };

    private static Bug Bug(Attachment[]? attachments, Comment[]? comments) => new()
    {
        Id = 1,
        ReportId = 1,
        Title = "баг",
        Receive = "получили",
        Expect = null,
        CreatedAt = Moment,
        UpdatedAt = Moment,
        CreatorUserId = "user-1",
        Status = 0,
        CreatorType = 0,
        Attachments = attachments,
        Comments = comments,
    };

    private static Comment Comment() => new()
    {
        Id = 1,
        BugId = 1,
        Text = "комментарий",
        CreatorUserId = "user-1",
        CreatorType = 0,
        Audience = 0,
        CreatedAt = Moment,
        UpdatedAt = Moment,
    };

    private static Attachment Attachment(bool? hasPreview = true) => new()
    {
        Id = 10,
        EntityId = 1,
        AttachType = 0,
        StorageKey = "reports/1/shot.png",
        StorageKind = 1,
        CreatedAt = Moment,
        CreatorUserId = "user-1",
        LengthBytes = 128,
        FileName = "shot.png",
        HasPreview = hasPreview,
    };
}
