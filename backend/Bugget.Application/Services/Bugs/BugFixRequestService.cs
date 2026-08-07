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

    /// <summary>
    /// Порог уборки протухших записей. Словарь живёт столько же, сколько процесс,
    /// и без уборки рос бы на запись за каждый когда-либо запрошенный баг.
    /// </summary>
    private const int SweepThreshold = 1024;

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

        var cooldownKey = $"{resolvedReport.Id}:{bugId}";
        if (!TryEnterCooldown(cooldownKey))
        {
            // Идемпотентный успех: запрос уже в работе, спамить нечем.
            return null;
        }

        try
        {
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
        }
        catch
        {
            // Кулдаун занимается до работы, чтобы не пустить второй клик внутрь.
            // Но если работа не сделалась, держать его нельзя: пользователь увидит
            // 500, нажмёт ещё раз — и следующую минуту получал бы «принято», за
            // которым ничего не стоит. Отказ обязан оставаться видимым.
            _cooldownUntil.TryRemove(cooldownKey, out _);
            throw;
        }

        return null;
    }

    private bool TryEnterCooldown(string key)
    {
        var now = timeProvider.GetUtcNow();
        var until = now.Add(Cooldown);

        if (_cooldownUntil.Count >= SweepThreshold)
        {
            SweepExpired(now);
        }

        // Атомарно: победитель гонки — тот, чья запись реально легла (TryAdd или
        // TryUpdate с comparand). Сравнение значений времени тут не годится: два
        // запроса в один тик получили бы одинаковый until и оба «победили» бы.
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

    /// <summary>
    /// Уборка отработавших записей. Удаляется только то, что успело протухнуть, и
    /// только если значение не подменили в гонке, — активный кулдаун снять нельзя.
    /// </summary>
    private void SweepExpired(DateTimeOffset now)
    {
        foreach (var (key, until) in _cooldownUntil)
        {
            if (until <= now)
            {
                _cooldownUntil.TryRemove(new KeyValuePair<string, DateTimeOffset>(key, until));
            }
        }
    }
}
