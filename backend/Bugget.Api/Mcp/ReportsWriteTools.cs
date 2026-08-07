using System.ComponentModel;
using Bugget.Application.Commands.Bug;
using Bugget.Application.Commands.Comment;
using Bugget.Application.Commands.Report;
using Bugget.Application.Mappers;
using Bugget.Application.Options;
using Bugget.Application.Services;
using Bugget.Application.Services.Bugs;
using Bugget.Application.Services.Comments;
using Bugget.Application.Services.Reports;
using Bugget.Domain.Authentication;
using Bugget.Domain.Errors;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Bugget.Api.Mcp;

/// <summary>
/// Write-инструменты MCP над репортами: статус репорта, содержимое бага,
/// комментарии. Ровно та поверхность, которой агент отчитывается о починке;
/// создание репортов и багов — сознательно вне MVP.
///
/// Адаптер того же рода, что <see cref="ReportsReadTools"/>: identity — из
/// запроса, изоляция workspace/team — в application-сервисах, как у REST.
/// Атрибуты валидации DTO здесь срабатывать некому (MVC-биндинга нет), поэтому
/// те же границы длин проверяются явно до вызова сервиса.
///
/// Кто автор записи, инструменты не решают: сервисы штампуют
/// <see cref="Bugget.Domain.Common.CreatorType"/> из
/// <see cref="UserIdentity.ActorCreatorType"/>, а тот выводится из способа
/// аутентификации запроса — запись, пришедшая по PAT, видна в истории как
/// действие агента без единой строки кода в этом классе.
/// </summary>
[McpServerToolType]
internal sealed class ReportsWriteTools(
    IReportsService reportsService,
    IBugsService bugsService,
    ICommentsService commentsService,
    IHttpContextAccessor httpContextAccessor,
    IOptions<ReportAliasOptions> aliasOptions)
{
    /// <summary>
    /// Потолок write-вызовов на пользователя: сошедший с ума или скомпрометированный
    /// агент не зальёт репорты сотнями правок, а нормальной работе (несколько правок
    /// и комментариев на баг) лимит не виден. Static: инструменты создаются на вызов,
    /// окно живёт с процессом.
    /// </summary>
    private static readonly FixedWindowLimiter WriteLimiter =
        new(TimeProvider.System, limit: 30, window: TimeSpan.FromMinutes(1));

    [McpServerTool(Name = "patch_report", Idempotent = true, OpenWorld = false)]
    [Description(
        "Перевести репорт в другой статус. Отвечает обновлённым репортом: статус, " +
        "ответственный, момент изменения.")]
    public async Task<string> PatchReportAsync(
        [Description("Идентификатор репорта из list_reports или get_report.")] string reportId,
        [Description("Новый статус: backlog, resolved, fix, rejected, test.")] string status)
    {
        var (result, error) = await reportsService.PatchReportAsync(
            reportId,
            CurrentUser(),
            new ReportPatchDto { Status = McpWire.ParseReportStatus(status) });

        var patched = Unwrap(result, error);
        var view = patched.ToPatchResultViewModel(aliasOptions.Value);

        return McpWire.Serialize(new McpReportPatchResult(
            view.Id,
            view.Title,
            McpWire.FormatReportStatus(view.Status),
            view.ResponsibleUserId,
            view.PastResponsibleUserId,
            view.UpdatedAt));
    }

    [McpServerTool(Name = "patch_bug", Idempotent = true, OpenWorld = false)]
    [Description(
        "Изменить баг: статус и/или текст (заголовок, что получили, что ожидали). " +
        "Поля, которые не переданы, не меняются; хотя бы одно обязано быть передано.")]
    public async Task<string> PatchBugAsync(
        [Description("Идентификатор репорта, в котором лежит баг.")] string reportId,
        [Description("Идентификатор бага из get_report.")] int bugId,
        [Description("Новый статус: open, verified, rejected, fixed.")] string? status = null,
        [Description("Новый заголовок, от 1 до 128 символов.")] string? title = null,
        [Description("Что получили по факту, от 1 до 2048 символов.")] string? receive = null,
        [Description("Что ожидали, от 1 до 2048 символов.")] string? expect = null)
    {
        if (status is null && title is null && receive is null && expect is null)
        {
            throw new McpException(
                "Передайте хотя бы одно поле: status, title, receive или expect — пустой патч ничего не меняет.");
        }

        ValidateLength(title, 128, "title");
        ValidateLength(receive, 2048, "receive");
        ValidateLength(expect, 2048, "expect");

        var (result, error) = await bugsService.PatchBugAsync(
            CurrentUser(),
            reportId,
            bugId,
            new BugPatchDto
            {
                Title = title,
                Receive = receive,
                Expect = expect,
                Status = status is null ? null : McpWire.ParseBugStatus(status),
            });

        var patched = Unwrap(result, error);

        return McpWire.Serialize(new McpBugPatchResult(
            patched.Id,
            patched.Title,
            McpWire.FormatBugStatus(patched.Status),
            patched.Receive,
            patched.Expect,
            patched.UpdatedAt));
    }

    [McpServerTool(Name = "create_comment", OpenWorld = false)]
    [Description(
        "Оставить комментарий к багу — основной способ рассказать, что и как исправлено. " +
        "Отвечает созданным комментарием.")]
    public async Task<string> CreateCommentAsync(
        [Description("Идентификатор репорта, в котором лежит баг.")] string reportId,
        [Description("Идентификатор бага из get_report.")] int bugId,
        [Description("Текст комментария, от 1 до 2048 символов.")] string text,
        [Description("Кому виден: internal (только команде, по умолчанию) или external.")]
        string? audience = null)
    {
        var (comment, error) = await commentsService.CreateCommentAsync(
            CurrentUser(),
            reportId,
            bugId,
            BuildCommentDto(text, audience));

        return McpWire.Serialize(ToComment(Unwrap(comment, error)));
    }

    [McpServerTool(Name = "update_comment", Idempotent = true, OpenWorld = false)]
    [Description("Изменить текст своего комментария. Отвечает обновлённым комментарием.")]
    public async Task<string> UpdateCommentAsync(
        [Description("Идентификатор репорта, в котором лежит баг.")] string reportId,
        [Description("Идентификатор бага из get_report.")] int bugId,
        [Description("Идентификатор комментария из get_report.")] int commentId,
        [Description("Новый текст, от 1 до 2048 символов.")] string text,
        [Description("Кому виден: internal (только команде) или external.")] string? audience = null)
    {
        var (comment, error) = await commentsService.UpdateCommentAsync(
            CurrentUser(),
            reportId,
            bugId,
            commentId,
            BuildCommentDto(text, audience));

        return McpWire.Serialize(ToComment(Unwrap(comment, error)));
    }

    private static CommentDto BuildCommentDto(string text, string? audience)
    {
        ValidateLength(text, 2048, "text");

        return new CommentDto
        {
            Text = text,
            Audience = audience is null ? null : (short)McpWire.ParseAudience(audience),
        };
    }

    private static McpComment ToComment(Bugget.Domain.Comments.CommentSummary comment) => new(
        comment.Id,
        comment.Text,
        comment.CreatorUserId,
        McpWire.FormatCreatorType(comment.CreatorType),
        McpWire.FormatAudience(comment.Audience),
        comment.CreatedAt,
        Attachments: null);

    private UserIdentity CurrentUser()
    {
        var user = httpContextAccessor.HttpContext?.User.GetIdentity()
            ?? throw new McpException("Запрос пришёл без контекста пользователя.");

        if (!WriteLimiter.TryAcquire(user.Id))
        {
            throw new McpException(
                "Слишком много правок за минуту. Подождите и продолжите — лимит защищает репорты от случайного потока изменений.");
        }

        return user;
    }

    /// <summary>
    /// Отказ сервиса уходит модели заголовком прикладной ошибки — тем же текстом,
    /// что REST кладёт в problem+json. «Нет» и «не твой» здесь уже неразличимы:
    /// это гарантия сервисов, инструмент её просто не портит.
    /// </summary>
    private static T Unwrap<T>(T? value, Error? error) where T : class =>
        error is null && value is not null
            ? value
            : throw new McpException(error?.Title ?? "Операция не выполнена.");

    /// <summary>
    /// Те же границы, что у REST в атрибутах DTO: без MVC-биндинга атрибут — просто
    /// украшение, а ослаблять валидацию относительно REST инструментам нельзя.
    /// </summary>
    private static void ValidateLength(string? value, int max, string parameter)
    {
        if (value is { Length: 0 })
        {
            throw new McpException($"Параметр {parameter} не может быть пустой строкой.");
        }

        if (value is not null && value.Length > max)
        {
            throw new McpException($"Параметр {parameter} длиннее {max} символов.");
        }
    }
}
