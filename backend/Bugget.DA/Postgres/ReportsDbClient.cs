using Bugget.BO.Ports;
using Bugget.DA.Transactions;
using Bugget.Entities.BO;
using Bugget.Entities.BO.AttachmentBo;
using Bugget.Entities.BO.Bugs;
using Bugget.Entities.BO.Comments;
using Bugget.Entities.BO.ReportBo;
using Bugget.Entities.BO.Search;
using Bugget.Entities.DTO.Report;
using Dapper;

namespace Bugget.DA.Postgres;

public sealed class ReportsDbClient : PostgresClient, IReportsDbClient
{
    /// <summary>
    /// Получает отчет по ID.
    /// </summary>
    public async Task<Report?> GetReportInternalAsync(int reportId)
    {
        await using var conn = await DataSource.OpenConnectionAsync();

        // Получаем отчет по уже разрешенному ID
        var report = await conn.QuerySingleOrDefaultAsync<Report>(
            "SELECT * FROM public.get_report_internal(@reportId);",
            new { reportId });

        if (report == null)
        {
            return null;
        }

        // Теперь используем id отчета для получения дочерних сущностей
        await using var multi = await conn.QueryMultipleAsync(@"
  SELECT * FROM public.list_bugs_internal(@reportIds);
  SELECT * FROM public.list_participants_internal(@reportIds);
  SELECT * FROM public.list_comments_internal(@reportIds);
  SELECT * FROM public.list_attachments_internal(@reportIds);
  SELECT * FROM public.list_bug_steps_internal(@reportIds);
  SELECT * FROM public.list_report_links_internal(@reportIds);
", new { reportIds = new[] { report.Id } });

        // 2. Дочерние сущности
        report.Bugs = (await multi.ReadAsync<Bug>()).ToArray();
        var participants = await multi.ReadAsync<(int ReportId, string UserId)>();
        report.ParticipantsUserIds = participants.Select(p => p.UserId).ToArray();
        var comments = (await multi.ReadAsync<Comment>()).ToArray();
        var attachments = (await multi.ReadAsync<Attachment>()).ToArray();
        var bugSteps = (await multi.ReadAsync<BugStepSummary>()).ToArray();
        var reportLinks = (await multi.ReadAsync<ReportLink>()).ToArray();

        // 3. Группируем по багам
        var commentsByBug = comments.GroupBy(c => c.BugId).ToDictionary(g => g.Key, g => g.ToArray());
        var attachmentsByEntity = attachments.GroupBy(a => a.EntityId).ToDictionary(g => g.Key, g => g.ToArray());
        var stepsByBug = bugSteps.GroupBy(s => s.BugId).ToDictionary(g => g.Key, g => g.ToArray());

        foreach (var bug in report.Bugs)
        {
            bug.Comments = commentsByBug.TryGetValue(bug.Id, out var c) ? c : [];
            bug.Attachments = attachmentsByEntity.TryGetValue(bug.Id, out var a)
            ? a.Where(a => a.AttachType == (int)AttachType.Fact || a.AttachType == (int)AttachType.Expected).ToArray()
            : [];
            bug.Steps = stepsByBug.TryGetValue(bug.Id, out var s) ? s : [];
            foreach (var step in bug.Steps ?? [])
            {
                step.Attachments = attachmentsByEntity.TryGetValue(step.Id, out var stepAttachments)
                ? stepAttachments.Where(a => a.AttachType == (int)AttachType.BugStep).ToArray()
                : [];
            }
        }

        foreach (var comment in comments)
        {
            comment.Attachments = attachmentsByEntity.TryGetValue(comment.Id, out var a)
            ? a.Where(a => a.AttachType == (int)AttachType.Comment).ToArray()
            : [];
        }

        report.Links = reportLinks;

        return report;
    }

    public async Task<(long total, Report[] reports)> ListReportsAsync(
    string? organizationId, string? userId, string? teamId, int[]? statuses, int[]? creatorTypes, int skip, int take)
    {
        var (total, ids) = await ListReportIdsAsync(organizationId, userId, teamId, statuses, creatorTypes, skip, take);
        if (ids.Length == 0)
        {
            return (total, Array.Empty<Report>());
        }

        await using var conn = await DataSource.OpenConnectionAsync();

        const string sql = @"
        SELECT * FROM public.list_reports_internal(@ids);

        SELECT * FROM public.list_participants_internal(@ids);

        SELECT * FROM public.list_bugs_internal(@ids);

        SELECT * FROM public.list_comments_internal(@ids);
    ";

        await using var grid = await conn.QueryMultipleAsync(sql, new { ids });

        var reports = (await grid.ReadAsync<Report>()).ToArray();

        // Восстанавливаем порядок сортировки из ids
        var reportDict = reports.ToDictionary(r => r.Id);
        reports = ids.Select(id => reportDict.GetValueOrDefault(id)).Where(r => r != null).ToArray()!;

        var participants = await grid.ReadAsync<(int ReportId, string UserId)>();
        var participantsByReport = participants
            .GroupBy(x => x.ReportId)
            .ToDictionary(g => g.Key, g => g.Select(z => z.UserId).ToArray());

        var bugs = (await grid.ReadAsync<Bug>()).ToArray();
        var bugsByReport = bugs
            .GroupBy(b => b.ReportId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        var comments = (await grid.ReadAsync<Comment>()).ToArray();
        var commentsByBug = comments
            .GroupBy(c => c.BugId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        // сборка
        foreach (var r in reports)
        {
            r.ParticipantsUserIds = participantsByReport.GetValueOrDefault(r.Id) ?? Array.Empty<string>();

            var rb = bugsByReport.GetValueOrDefault(r.Id) ?? Array.Empty<Bug>();
            foreach (var b in rb)
            {
                b.Comments = commentsByBug.GetValueOrDefault(b.Id) ?? Array.Empty<Comment>();
            }
            r.Bugs = rb;
        }

        return (total, reports);
    }

    /// <summary>
    /// Создает новый отчет и возвращает его краткую структуру.
    /// </summary>
    public async Task<ReportSummary> CreateReportAsync(string userId, string? teamId, string? organizationId, ReportCreateDto dto)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<ReportSummary>(
            "SELECT * FROM public.create_report_v3(@userId, @title, @teamId, @organizationId);",
            new { userId, title = dto.Title, teamId, organizationId }
        );
    }

    public Task<ReportSummary> CreateReportAsync(
        ITransactionScope scope,
        string userId,
        string? teamId,
        string? organizationId,
        string? title,
        short creatorType)
    {
        var (connection, tx) = scope.Unwrap();
        return connection.QuerySingleAsync<ReportSummary>(new CommandDefinition(
            "SELECT * FROM public.create_report_v3(@userId, @title, @teamId, @organizationId, @creatorType);",
            new { userId, title, teamId, organizationId, creatorType },
            transaction: tx));
    }

    /// <summary>
    /// Возвращает текущий статус отчёта в рамках переданной транзакции.
    /// Используется Internal*-сервисами для I-3 проверки (не дописывать в закрытый отчёт).
    /// </summary>
    public Task<int?> GetStatusInternalAsync(
        ITransactionScope scope,
        int reportId,
        CancellationToken ct = default)
    {
        var (connection, tx) = scope.Unwrap();
        return connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT status FROM public.get_report_internal(@reportId);",
            new { reportId },
            transaction: tx,
            cancellationToken: ct));
    }

    /// <inheritdoc />
    public async Task<(int Status, string? ResponsibleUserId)?> GetStatusAndResponsibleAsync(
        ITransactionScope scope,
        int reportId,
        CancellationToken ct = default)
    {
        var (connection, tx) = scope.Unwrap();
        var row = await connection.QuerySingleOrDefaultAsync<ReportStatusResponsibleRow?>(new CommandDefinition(
            "SELECT status, responsible_user_id FROM public.get_report_internal(@reportId);",
            new { reportId },
            transaction: tx,
            cancellationToken: ct));
        return row is null ? null : (row.Status, row.ResponsibleUserId);
    }

    /// <inheritdoc />
    public Task<bool?> GetIsExcludedFromAnalyticsAsync(
        ITransactionScope scope,
        int reportId,
        CancellationToken ct = default)
    {
        var (connection, tx) = scope.Unwrap();
        // P1.2 (review): SELECT ... FOR UPDATE сериализует concurrent PATCH-toggle
        // в рамках одной строки. Без блокировки два параллельных PATCH с разными
        // значениями могли прочитать одно и то же исходное состояние, оба считать
        // переход «изменение» и оба эмитнуть `excluded_from_analytics_toggled` —
        // дубль события. FOR UPDATE удерживает row lock до конца транзакции
        // (UnitOfWork.ExecuteAsync), второй PATCH ждёт, читает уже обновлённое
        // значение и корректно решает no-op vs emit.
        //
        // Нужен row lock, поэтому читаем прямо из reports с FOR UPDATE.
        return connection.QuerySingleOrDefaultAsync<bool?>(new CommandDefinition(
            "SELECT is_excluded_from_analytics FROM public.reports WHERE id = @reportId FOR UPDATE;",
            new { reportId },
            transaction: tx,
            cancellationToken: ct));
    }

    /// <inheritdoc />
    public async Task<bool?> GetIsExcludedFromAnalyticsAsync(
        int reportId,
        CancellationToken ct = default)
    {
        // Нетранзакционный SELECT без FOR UPDATE: эмиссия события
        // `excluded_from_analytics_toggled` чисто аудитная, projection её не читает.
        // Гонка между параллельными PATCH здесь даст максимум дубль/потерю одной
        // строки domain_events — допустимо (бета-фича TECHSPEC §4.5).
        await using var connection = await DataSource.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<bool?>(new CommandDefinition(
            "SELECT is_excluded_from_analytics FROM public.reports WHERE id = @reportId;",
            new { reportId },
            cancellationToken: ct));
    }

    private sealed class ReportStatusResponsibleRow
    {
        public int Status { get; init; }
        public string? ResponsibleUserId { get; init; }
    }

    /// <summary>
    /// Список репортов тестера для команды <c>/my</c> (TECHSPEC §4.3.5).
    /// I-11: возвращается только Bug, созданный этим <c>creatorUserId</c>+<c>creatorType</c>.
    /// </summary>
    public async Task<ReportListItem[]> ListByCreatorInternalAsync(
        string organizationId,
        string creatorUserId,
        short creatorType,
        int limit,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                r.id            AS report_id,
                r.team_report_id AS report_number,
                r.title         AS report_title,
                r.status        AS report_status,
                r.created_at    AS submitted_at,
                b.id            AS bug_id,
                b.title         AS bug_title,
                b.status        AS bug_status
            FROM public.reports r
            JOIN public.bugs b ON b.report_id = r.id
            WHERE r.creator_user_id = @creatorUserId
              AND r.creator_type    = @creatorType
              AND r.creator_organization_id = @organizationId
              AND b.creator_user_id = @creatorUserId
              AND b.creator_type    = @creatorType
            ORDER BY r.created_at DESC, r.id DESC
            LIMIT @limit;";

        await using var connection = await DataSource.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<ReportListItem>(new CommandDefinition(
            sql,
            new { organizationId, creatorUserId, creatorType, limit },
            cancellationToken: ct));
        return rows.ToArray();
    }

    /// <summary>
    /// Обновляет краткую информацию об отчете и возвращает его краткую структуру.
    /// Если <paramref name="scope"/> передан — операция идёт в его транзакции
    /// (для эмиссии domain events в той же транзакции, что и UPDATE); иначе клиент
    /// открывает собственное соединение.
    /// </summary>
    public async Task<ReportPatchResult> PatchReportAsync(
        int reportId,
        ReportPatchDto dto,
        ITransactionScope? scope = null,
        CancellationToken ct = default)
    {
        const string sql =
            "SELECT * FROM public.patch_report_internal(@report_id, @title, @status, @responsible_user_id, @is_excluded_from_analytics);";
        var parameters = new
        {
            report_id = reportId,
            title = dto.Title,
            status = dto.Status,
            responsible_user_id = dto.ResponsibleUserId,
            is_excluded_from_analytics = dto.IsExcludedFromAnalytics
        };

        if (scope is null)
        {
            await using var connection = await DataSource.OpenConnectionAsync(ct);
            return await connection.QuerySingleAsync<ReportPatchResult>(new CommandDefinition(
                sql,
                parameters,
                cancellationToken: ct));
        }

        var (txConnection, tx) = scope.Unwrap();
        return await txConnection.QuerySingleAsync<ReportPatchResult>(new CommandDefinition(
            sql,
            parameters,
            transaction: tx,
            cancellationToken: ct));
    }

    public async Task<(long total, Report[] reports)> SearchReportsAsync(SearchReports search)
    {
        var (total, ids) = await SearchReportIdsAsync(search);
        if (ids.Length == 0)
        {
            return (total, Array.Empty<Report>());
        }

        await using var conn = await DataSource.OpenConnectionAsync();

        const string sql = @"
        SELECT * FROM public.list_reports_internal(@ids);
        SELECT * FROM public.list_participants_internal(@ids);
        SELECT * FROM public.list_bugs_internal(@ids);
        SELECT * FROM public.list_comments_internal(@ids);
    ";

        await using var grid = await conn.QueryMultipleAsync(sql, new { ids });

        var reports = (await grid.ReadAsync<Report>()).ToArray();

        // Восстанавливаем порядок сортировки из ids
        var reportDict = reports.ToDictionary(r => r.Id);
        reports = ids.Select(id => reportDict.GetValueOrDefault(id)).Where(r => r != null).ToArray()!;

        var participants = await grid.ReadAsync<(int ReportId, string UserId)>();
        var participantsByReport = participants
            .GroupBy(x => x.ReportId)
            .ToDictionary(g => g.Key, g => g.Select(z => z.UserId).ToArray());

        var bugs = (await grid.ReadAsync<Bug>()).ToArray();
        var bugsByReport = bugs
            .GroupBy(b => b.ReportId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        var comments = (await grid.ReadAsync<Comment>()).ToArray();
        var commentsByBug = comments
            .GroupBy(c => c.BugId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        foreach (var r in reports)
        {
            r.ParticipantsUserIds = participantsByReport.GetValueOrDefault(r.Id) ?? Array.Empty<string>();

            var rb = bugsByReport.GetValueOrDefault(r.Id) ?? Array.Empty<Bug>();
            foreach (var b in rb)
            {
                b.Comments = commentsByBug.GetValueOrDefault(b.Id) ?? Array.Empty<Comment>();
            }
            r.Bugs = rb;
        }

        return (total, reports);
    }

    private async Task<(int total, int[] ids)> SearchReportIdsAsync(SearchReports search)
    {
        await using var conn = await DataSource.OpenConnectionAsync();

        const string sql = @"
        SELECT public.search_reports_count(
            @query,
            @statuses::int[],
            @userIds::text[],
            @organizationId::text,
            @teamId::text,
            @creatorTypes::smallint[]
        );

        SELECT id FROM public.search_reports_ids(
            @sortField::text, @sortDesc, @skip, @take,
            @query,
            @statuses::int[],
            @userIds::text[],
            @organizationId::text,
            @teamId::text,
            @creatorTypes::smallint[]
        );
    ";

        await using var grid = await conn.QueryMultipleAsync(sql, new
        {
            sortField = search.Sort.Field,            // "rank" | "created" | "updated"
            sortDesc = search.Sort.IsDescending,
            skip = (int)search.Skip,
            take = (int)search.Take,
            query = search.Query,
            statuses = search.ReportStatuses,        // int[]
            userIds = search.UserIds,               // string[]
            organizationId = search.OrganizationId,
            teamId = search.TeamId,
            creatorTypes = search.CreatorTypes       // smallint[]
        });

        var total = await grid.ReadSingleAsync<int>();
        var ids = (await grid.ReadAsync<int>()).ToArray();

        return (total, ids);
    }

    public async Task<long> CountReportsAsync(
        string? organizationId,
        string? teamId,
        int[]? statuses,
        short[]? creatorTypes,
        CancellationToken ct = default)
    {
        await using var conn = await DataSource.OpenConnectionAsync(ct);

        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            @"SELECT public.search_reports_count(
                NULL,
                @statuses::int[],
                NULL,
                @organizationId::text,
                @teamId::text,
                @creatorTypes::smallint[]
            );",
            new { organizationId, teamId, statuses, creatorTypes },
            cancellationToken: ct));
    }

    public async Task ChangeStatusAsync(int reportId, int newStatus)
    {
        await using var connection = await DataSource.OpenConnectionAsync();

        await connection.ExecuteAsync(
            "SELECT public.change_status_internal(@report_id, @new_status);",
            new { report_id = reportId, new_status = newStatus }
        );
    }

    /// <summary>
    /// Tx-aware вариант <see cref="ChangeStatusAsync(int, int)"/>: выполняется в
    /// переданной транзакции вместе с публикацией domain event.
    /// </summary>
    public Task ChangeStatusAsync(
        ITransactionScope scope,
        int reportId,
        int newStatus,
        CancellationToken ct = default)
    {
        var (connection, tx) = scope.Unwrap();
        return connection.ExecuteAsync(new CommandDefinition(
            "SELECT public.change_status_internal(@report_id, @new_status);",
            new { report_id = reportId, new_status = newStatus },
            transaction: tx,
            cancellationToken: ct));
    }

    public async Task<ResolvedReportId?> ResolveReportIdAsync(
        string? workspaceId,
        string? teamId,
        int? reportId,
        Guid? publicId,
        int? teamReportId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<ResolvedReportId?>(
            "SELECT * FROM public.resolve_report_id(@workspaceId, @teamId, @reportId, @publicId, @teamReportId);",
            new { workspaceId, teamId, reportId, publicId, teamReportId }
        );
    }

    private async Task<(long total, int[] ids)> ListReportIdsAsync(
    string? organizationId, string? userId, string? teamId, int[]? statuses, int[]? creatorTypes, int skip, int take)
    {
        await using var conn = await DataSource.OpenConnectionAsync();

        const string sql = @"
        SELECT public.list_reports_count(@organizationId, @userId, @teamId, @statuses, @creatorTypes);
        SELECT id FROM public.list_reports_ids(@skip, @take, @organizationId, @userId, @teamId, @statuses, @creatorTypes);";

        await using var grid = await conn.QueryMultipleAsync(sql, new
        {
            organizationId,
            userId,
            teamId,
            statuses,
            creatorTypes,
            skip,
            take
        });

        var total = await grid.ReadSingleAsync<long>();
        var ids = (await grid.ReadAsync<int>()).ToArray();
        return (total, ids);
    }
}
