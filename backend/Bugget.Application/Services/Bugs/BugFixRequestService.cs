using System.Collections.Concurrent;
using Bugget.Application.Errors;
using Bugget.Application.Ports;
using Bugget.Application.Services.Comments;
using Bugget.Application.Services.Reports;
using Bugget.Domain.Authentication;
using Bugget.Domain.Errors;
using Bugget.Domain.Reports;

namespace Bugget.Application.Services.Bugs;

/// <summary>
/// Продуктовый триггер «попросить агента починить баг»: системный
/// комментарий-маркер в баге (уходит по realtime, как остальные лог-комментарии)
/// и асинхронный сигнал раннеру через порт. Модели внутри bugget нет — только
/// маркер и сигнал.
/// </summary>
public sealed class BugFixRequestService(
    IReportsService reportsService,
    IBugsService bugsService,
    CommentLogsService commentLogsService,
    IBugFixRequestedNotifier notifier,
    ITaskQueue taskQueue,
    TimeProvider timeProvider) : IBugFixRequestService
{
    /// <summary>
    /// Кулдаун повторного запроса по тому же багу: двойной клик и вторая вкладка
    /// не плодят комментарии и вебхуки. Состояние процесса, а не БД: self-hosted
    /// контур одноинстансный, а после рестарта лишний одиночный повтор безвреден.
    /// </summary>
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _cooldownUntil = new();

    public async Task<Error?> RequestFixAsync(UserIdentity user, string aliasId, int bugId)
    {
        var resolvedReport = await reportsService.ResolveReportByAliasAsync(aliasId, user);
        if (resolvedReport == null)
        {
            return BoErrors.ReportNotFoundError;
        }

        var bug = await bugsService.GetBugAsync(resolvedReport.Id, bugId);
        if (bug == null)
        {
            return BoErrors.BugNotFoundError;
        }

        if (!TryEnterCooldown($"{resolvedReport.Id}:{bugId}"))
        {
            // Идемпотентный успех: запрос уже в работе, спамить нечем.
            return null;
        }

        var reportIdContext = new ReportIdContext(resolvedReport.Id, aliasId, resolvedReport.CreatorTeamId);
        await commentLogsService.LogFixRequestedAsync(reportIdContext, bugId, user);

        var payload = new BugFixRequestedPayload(
            user.OrganizationId,
            user.TeamId,
            aliasId,
            bugId,
            user.Id,
            $"/api/app/workspaces/{user.OrganizationId}/teams/{user.TeamId}/v2/reports/{aliasId}");
        await taskQueue.EnqueueAsync(() => notifier.NotifyAsync(payload, CancellationToken.None));

        return null;
    }

    private bool TryEnterCooldown(string key)
    {
        var now = timeProvider.GetUtcNow();
        var until = now.Add(Cooldown);

        // Атомарно: победитель гонки — тот, чья запись реально легла (TryAdd или
        // TryUpdate с comparand). Сравнение значений времени тут не годится: два
        // запроса в один тик получили бы одинаковый until и оба «победили» бы.
        // Протухшие записи перезаписываются по месту, отдельной чистки не нужно.
        while (true)
        {
            if (_cooldownUntil.TryAdd(key, until))
            {
                return true;
            }

            if (!_cooldownUntil.TryGetValue(key, out var existing))
            {
                continue;
            }

            if (existing > now)
            {
                return false;
            }

            if (_cooldownUntil.TryUpdate(key, until, existing))
            {
                return true;
            }
        }
    }
}

public interface IBugFixRequestService
{
    /// <summary><c>null</c> — принято (в том числе идемпотентный повтор в кулдауне).</summary>
    Task<Error?> RequestFixAsync(UserIdentity user, string aliasId, int bugId);
}
