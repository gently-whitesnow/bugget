using System.ComponentModel;
using Bugget.Application.Mappers;
using Bugget.Application.Options;
using Bugget.Application.Services.Reports;
using Bugget.Domain.Authentication;
using Bugget.Domain.Reports;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Bugget.Api.Mcp;

/// <summary>
/// Read-инструменты MCP над репортами.
///
/// Адаптер того же рода, что контроллер: разбирает аргументы, зовёт
/// application-сервис, приводит ответ к проводу. Изоляция данных здесь не
/// повторяется и не ослабляется — workspace приходит из identity запроса и
/// уходит в сервис тем же параметром, что из REST, а режет по нему SQL. Ни один
/// аргумент инструмента задать workspace не может.
/// </summary>
[McpServerToolType]
internal sealed class ReportsReadTools(
    IReportsService reportsService,
    IHttpContextAccessor httpContextAccessor,
    IOptions<ReportAliasOptions> aliasOptions,
    McpAttachmentContent attachmentContent)
{
    /// <summary>
    /// Потолок страницы. REST его для поиска не ставит, но там за ответом человек
    /// со скроллом, а здесь — окно контекста, в которое ответ должен поместиться
    /// целиком.
    /// </summary>
    private const int MaxTake = 100;

    [McpServerTool(Name = "list_reports", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Список репортов рабочего пространства, свежие сверху. Без фильтров возвращает последние. " +
        "Содержимое багов не отдаёт — за ним get_report.")]
    public async Task<string> ListReportsAsync(
        [Description("Только репорты, где этот пользователь автор или участник.")] string? userId = null,
        [Description("Только репорты этой команды.")] string? teamId = null,
        [Description("Статусы репорта: backlog, resolved, fix, rejected, test.")] string[]? reportStatuses = null,
        [Description("Типы автора: user, system, tg_beta_tester, agent.")] string[]? creatorTypes = null,
        [Description("Сколько записей пропустить. По умолчанию 0.")] int skip = 0,
        [Description("Сколько записей вернуть, от 1 до 100. По умолчанию 10.")] int take = 10)
    {
        ValidatePaging(skip, take);

        var (total, reports) = await reportsService.ListReportsAsync(
            CurrentUser().OrganizationId,
            userId,
            teamId,
            McpWire.ParseReportStatuses(reportStatuses),
            McpWire.ParseCreatorTypes(creatorTypes),
            skip,
            take);

        return McpWire.Serialize(
            McpReportMapper.ToPage(total, skip, take, reports.ToViewModel(aliasOptions.Value)));
    }

    [McpServerTool(Name = "search_reports", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Поиск репортов по тексту заголовка и содержимого багов. Отвечает тем же списком, что list_reports.")]
    public async Task<string> SearchReportsAsync(
        [Description("Поисковый запрос. Пусто — обычная выборка по фильтрам.")] string? query = null,
        [Description("Статусы репорта: backlog, resolved, fix, rejected, test.")] string[]? reportStatuses = null,
        [Description("Только репорты, где этот пользователь автор или участник.")] string? userId = null,
        [Description("Только репорты этой команды.")] string? teamId = null,
        [Description("Типы автора: user, system, tg_beta_tester, agent.")] string[]? creatorTypes = null,
        [Description("Сортировка: created_desc, created_asc, updated_desc, updated_asc, rank_desc, rank_asc.")]
        string? sort = null,
        [Description("Сколько записей пропустить. По умолчанию 0.")] int skip = 0,
        [Description("Сколько записей вернуть, от 1 до 100. По умолчанию 10.")] int take = 10)
    {
        ValidatePaging(skip, take);

        var creatorTypeValues = McpWire.ParseCreatorTypes(creatorTypes);
        var (total, reports) = await reportsService.SearchReportsAsync(ReportMapper.ToSearchReports(
            query,
            McpWire.ParseReportStatuses(reportStatuses),
            userId,
            teamId,
            CurrentUser().OrganizationId,
            sort,
            (uint)skip,
            (uint)take,
            creatorTypeValues?.Select(value => (short)value).ToArray()));

        return McpWire.Serialize(
            McpReportMapper.ToPage(total, skip, take, reports.ToViewModel(aliasOptions.Value)));
    }

    [McpServerTool(Name = "get_report", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Репорт целиком: баги, шаги воспроизведения, комментарии и метаданные вложений.")]
    public async Task<string> GetReportAsync(
        [Description("Идентификатор репорта из списка.")] string reportId)
    {
        var report = await LoadReportAsync(reportId);
        return McpWire.Serialize(McpReportMapper.ToReport(report.ToViewModel(aliasOptions.Value)));
    }

    [McpServerTool(Name = "get_attachment", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Вложение: метаданные и содержимое. Картинки приходят превью, оригинал — по original=true. " +
        "Текст приходит как есть, страницей offset/maxChars. " +
        "Видео байтами не приходит никогда: кадр-превью и ссылка download_path для человека.")]
    public async Task<IEnumerable<ContentBlock>> GetAttachmentAsync(
        [Description("Идентификатор репорта, в котором лежит вложение.")] string reportId,
        [Description("Идентификатор вложения из ответа get_report.")] int attachmentId,
        [Description("Только для картинок: отдать оригинал вместо превью.")] bool original = false,
        [Description("Только для текста: с какого символа читать. По умолчанию 0.")] int offset = 0,
        [Description("Только для текста: сколько символов вернуть, от 1 до 50000. По умолчанию 20000.")]
        int maxChars = McpAttachmentContent.DefaultMaxChars)
    {
        McpAttachmentContent.ValidateTextPaging(offset, maxChars);

        var user = CurrentUser();
        var report = await LoadReportAsync(reportId);
        var view = report.ToViewModel(aliasOptions.Value);

        var located = McpReportMapper.FindAttachment(report, attachmentId)
            ?? throw new McpException($"Вложение {attachmentId} в репорте {reportId} не найдено.");

        var meta = McpReportMapper.ToAttachmentDetails(
            located.Attachment,
            view.Id,
            DownloadPath(user, view.Id, located));

        var blocks = new List<ContentBlock>
        {
            new TextContentBlock { Text = McpWire.Serialize(meta) },
        };
        blocks.AddRange(await attachmentContent.BuildAsync(user, view.Id, located, original, offset, maxChars));

        return blocks;
    }

    /// <summary>
    /// Внешний путь REST-скачивания — тот, по которому файл откроет человек в
    /// браузере: с префиксом nginx и workspace/team из identity текущего запроса
    /// (тот же принцип, что origin-relative Location в ADR-0011).
    /// </summary>
    private static string DownloadPath(UserIdentity user, string reportId, LocatedAttachment located)
    {
        var suffix = (Bugget.Domain.AttachType)located.Attachment.AttachType switch
        {
            Bugget.Domain.AttachType.Comment =>
                $"bugs/{located.BugId}/comments/{located.ParentId}/attachments/{located.Attachment.Id}/content",
            Bugget.Domain.AttachType.BugStep =>
                $"bugs/{located.BugId}/steps/{located.ParentId}/attachments/{located.Attachment.Id}/content",
            _ => $"bugs/{located.BugId}/attachments/{located.Attachment.Id}/content",
        };

        return $"/api/app/workspaces/{user.OrganizationId}/teams/{user.TeamId}/v2/reports/{reportId}/{suffix}";
    }

    private UserIdentity CurrentUser() =>
        httpContextAccessor.HttpContext?.User.GetIdentity()
        ?? throw new McpException("Запрос пришёл без контекста пользователя.");

    /// <summary>
    /// Причина отказа наружу не уходит: «нет такого репорта» и «репорт не твой»
    /// снаружи обязаны быть неразличимы, иначе перебор идентификаторов расскажет,
    /// что заведено в соседнем workspace.
    /// </summary>
    private async Task<Report> LoadReportAsync(string reportId)
    {
        var user = CurrentUser();
        var (report, _) = await reportsService.GetReportAsync(reportId, user.OrganizationId, user.TeamId);

        return report ?? throw new McpException($"Репорт {reportId} не найден.");
    }

    private static void ValidatePaging(int skip, int take)
    {
        if (skip < 0)
        {
            throw new McpException("Параметр skip не может быть отрицательным.");
        }

        if (take is < 1 or > MaxTake)
        {
            throw new McpException($"Параметр take должен быть от 1 до {MaxTake}.");
        }
    }
}
