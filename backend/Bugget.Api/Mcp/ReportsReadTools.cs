using System.ComponentModel;
using Bugget.Application.Mappers;
using Bugget.Application.Options;
using Bugget.Application.Services.Reports;
using Bugget.Domain.Authentication;
using Bugget.Domain.Reports;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
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
    IOptions<ReportAliasOptions> aliasOptions)
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
        "Метаданные вложения: имя файла, к чему приложено, есть ли превью. Содержимое файла не отдаёт.")]
    public async Task<string> GetAttachmentAsync(
        [Description("Идентификатор репорта, в котором лежит вложение.")] string reportId,
        [Description("Идентификатор вложения из ответа get_report.")] int attachmentId)
    {
        var report = await LoadReportAsync(reportId);
        var view = report.ToViewModel(aliasOptions.Value);

        var attachment = McpReportMapper.FindAttachment(report, attachmentId)
            ?? throw new McpException($"Вложение {attachmentId} в репорте {reportId} не найдено.");

        return McpWire.Serialize(McpReportMapper.ToAttachmentDetails(attachment, view.Id));
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
