using Bugget.DA.Interfaces;
using Bugget.Entities.BO.Analytics;
using Bugget.Entities.BO.Bugs;
using Bugget.Entities.BO.ReportBo;
using Dapper;

namespace Bugget.DA.Postgres;

/// <summary>
/// Read-side доступ к данным аналитики. Источник правды — read-model
/// <c>report_phase_intervals</c>, поддерживаемая <c>ReportPhaseProjectionHandler</c>.
/// JOIN с <c>reports</c> даёт фильтры по workspace / is_excluded_from_analytics /
/// терминальному статусу и тайтлы для top-листа.
///
/// <c>closed_at</c> репорта явно не хранится — выводим как
/// <c>MAX(report_phase_intervals.exited_at)</c> у репорта с terminal status:
/// projection при переходе в терминал закрывает активный интервал датой события.
/// </summary>
public sealed class AnalyticsDbClient : PostgresClient, IAnalyticsDbClient
{
    private const short PhaseTest = (short)ReportStatus.Test;
    private const short PhaseFix = (short)ReportStatus.Fix;
    private const int StatusResolved = (int)ReportStatus.Resolved;
    private const int StatusRejected = (int)ReportStatus.Rejected;

    // SQL-блок возвращает три result set'а: closed-in-period reports,
    // глобальные phase-buckets (TestInitial/TestRetest/Fix) и weekly trends.
    // Все стартуют с одинакового `closed_reports` (workspace + not-excluded +
    // terminal status + closed_at ∈ [from; to)) — чтобы быть консистентными
    // между собой. @teamId опционален: NULL = workspace-summary.
    private const string SummaryDataSql = @"
WITH closed_reports AS (
    SELECT
        r.id                                AS report_id,
        r.title                             AS title,
        r.created_at                        AS created_at,
        MAX(rpi.exited_at)                  AS closed_at,
        MIN(rpi.entered_at) FILTER (WHERE rpi.phase = @phaseTest) AS first_test_entered_at,
        COALESCE(SUM(CASE WHEN rpi.phase = @phaseTest THEN COALESCE(rpi.duration_seconds, 0) END), 0)::bigint
                                            AS test_duration_seconds,
        COALESCE(SUM(CASE WHEN rpi.phase = @phaseFix  THEN COALESCE(rpi.duration_seconds, 0) END), 0)::bigint
                                            AS fix_duration_seconds,
        COUNT(*) FILTER (WHERE rpi.phase = @phaseTest)::int AS test_intervals,
        COUNT(*) FILTER (WHERE rpi.phase = @phaseFix)::int  AS fix_intervals
    FROM public.reports r
    JOIN public.report_phase_intervals rpi ON rpi.report_id = r.id
    WHERE r.creator_organization_id = @workspaceId
      AND (@teamId::text IS NULL OR r.creator_team_id = @teamId)
      AND r.is_excluded_from_analytics = FALSE
      AND r.status IN (@statusResolved, @statusRejected)
    GROUP BY r.id, r.title, r.created_at
    HAVING MAX(rpi.exited_at) >= @from AND MAX(rpi.exited_at) < @to
)
SELECT report_id, title, created_at, closed_at, first_test_entered_at,
       test_intervals, fix_intervals, test_duration_seconds, fix_duration_seconds
FROM closed_reports;

-- Phase buckets глобально по всем closed-in-period репортам.
-- TestInitial = Test с regression_cycle_index=0,
-- TestRetest  = Test с regression_cycle_index >= 1,
-- Fix         = все Fix-интервалы.
-- ReportCount = число уникальных репортов в bucket'е (conditional denominator).
WITH closed_reports AS (
    SELECT r.id AS report_id
    FROM public.reports r
    JOIN public.report_phase_intervals rpi ON rpi.report_id = r.id
    WHERE r.creator_organization_id = @workspaceId
      AND (@teamId::text IS NULL OR r.creator_team_id = @teamId)
      AND r.is_excluded_from_analytics = FALSE
      AND r.status IN (@statusResolved, @statusRejected)
    GROUP BY r.id
    HAVING MAX(rpi.exited_at) >= @from AND MAX(rpi.exited_at) < @to
), buckets AS (
    SELECT
        CASE
            WHEN rpi.phase = @phaseTest AND rpi.regression_cycle_index = 0 THEN 0  -- TestInitial
            WHEN rpi.phase = @phaseTest AND rpi.regression_cycle_index >= 1 THEN 1 -- TestRetest
            WHEN rpi.phase = @phaseFix                                     THEN 2 -- Fix
        END                                  AS bucket,
        rpi.report_id                        AS report_id,
        COALESCE(rpi.duration_seconds, 0)    AS duration_seconds
    FROM public.report_phase_intervals rpi
    JOIN closed_reports cr ON cr.report_id = rpi.report_id
    WHERE rpi.exited_at IS NOT NULL
)
SELECT
    bucket                                AS bucket,
    COUNT(DISTINCT report_id)::int        AS report_count,
    COALESCE(SUM(duration_seconds), 0)::bigint AS total_duration_seconds
FROM buckets
WHERE bucket IS NOT NULL
GROUP BY bucket
ORDER BY bucket;

-- Weekly trends. ISO 8601: понедельник = старт недели.
-- to_char('IYYY-""W""IW') корректен на границе годов.
WITH closed_reports AS (
    SELECT
        r.id                            AS report_id,
        MAX(rpi.exited_at)              AS closed_at,
        COALESCE(SUM(CASE WHEN rpi.phase = @phaseTest THEN COALESCE(rpi.duration_seconds, 0) END), 0)::bigint
                                        AS test_duration_seconds,
        COALESCE(SUM(CASE WHEN rpi.phase = @phaseFix  THEN COALESCE(rpi.duration_seconds, 0) END), 0)::bigint
                                        AS fix_duration_seconds
    FROM public.reports r
    JOIN public.report_phase_intervals rpi ON rpi.report_id = r.id
    WHERE r.creator_organization_id = @workspaceId
      AND (@teamId::text IS NULL OR r.creator_team_id = @teamId)
      AND r.is_excluded_from_analytics = FALSE
      AND r.status IN (@statusResolved, @statusRejected)
    GROUP BY r.id
    HAVING MAX(rpi.exited_at) >= @from AND MAX(rpi.exited_at) < @to
)
SELECT
    to_char(date_trunc('week', closed_at), 'IYYY""-W""IW') AS iso_week,
    SUM(test_duration_seconds) / 86400.0           AS test_days,
    SUM(fix_duration_seconds)  / 86400.0           AS fix_days,
    COUNT(*)::int                                  AS reports_closed
FROM closed_reports
GROUP BY 1
ORDER BY 1;";

    public async Task<AnalyticsRawData> GetSummaryDataAsync(
        string workspaceId,
        string? teamId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var parameters = new
        {
            workspaceId,
            teamId,
            from,
            to,
            phaseTest = PhaseTest,
            phaseFix = PhaseFix,
            statusResolved = StatusResolved,
            statusRejected = StatusRejected,
        };

        await using var conn = await DataSource.OpenConnectionAsync(ct);
        await using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            SummaryDataSql, parameters, cancellationToken: ct));

        var closedReports = await ReadClosedReportsAsync(multi);
        var phaseAggregates = await ReadPhaseAggregatesAsync(multi);
        var phaseTrendsWeekly = await ReadPhaseTrendsWeeklyAsync(multi);

        return new AnalyticsRawData
        {
            ClosedReports = closedReports,
            PhaseAggregates = phaseAggregates,
            PhaseTrendsWeekly = phaseTrendsWeekly,
        };
    }

    private static async Task<IReadOnlyList<ClosedReportRow>> ReadClosedReportsAsync(SqlMapper.GridReader multi)
    {
        var rows = await multi.ReadAsync<ClosedReportSqlRow>();
        return rows
            .Select(r => new ClosedReportRow
            {
                ReportId = r.report_id,
                Title = r.title ?? string.Empty,
                CreatedAt = r.created_at,
                ClosedAt = r.closed_at,
                FirstTestEnteredAt = r.first_test_entered_at,
                TestIntervals = r.test_intervals,
                FixIntervals = r.fix_intervals,
                TestDurationSeconds = r.test_duration_seconds,
                FixDurationSeconds = r.fix_duration_seconds,
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<PhaseAggregateRow>> ReadPhaseAggregatesAsync(SqlMapper.GridReader multi)
    {
        var rows = await multi.ReadAsync<PhaseAggregateSqlRow>();
        return rows
            .Select(r => new PhaseAggregateRow
            {
                Bucket = (PhaseBucket)r.bucket,
                ReportCount = r.report_count,
                TotalDurationSeconds = r.total_duration_seconds,
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<PhaseTrendWeeklyBo>> ReadPhaseTrendsWeeklyAsync(SqlMapper.GridReader multi)
    {
        var rows = await multi.ReadAsync<PhaseTrendSqlRow>();
        return rows
            .Select(r => new PhaseTrendWeeklyBo
            {
                IsoWeek = r.iso_week,
                TestDays = r.test_days,
                FixDays = r.fix_days,
                ReportsClosed = r.reports_closed,
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<PhaseIntervalBo>?> GetReportTimelineAsync(
        string workspaceId,
        long reportId,
        CancellationToken ct)
    {
        const string sql = @"
SELECT EXISTS (
    SELECT 1
    FROM public.reports r
    WHERE r.id = @reportId
      AND r.creator_organization_id = @workspaceId
      AND r.is_excluded_from_analytics = FALSE
);

SELECT
    rpi.report_id              AS report_id,
    rpi.phase                  AS phase,
    rpi.entered_at             AS entered_at,
    rpi.exited_at              AS exited_at,
    rpi.regression_cycle_index AS regression_cycle_index
FROM public.report_phase_intervals rpi
WHERE rpi.report_id = @reportId
ORDER BY rpi.entered_at, rpi.id;";

        await using var conn = await DataSource.OpenConnectionAsync(ct);
        await using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { reportId = (int)reportId, workspaceId },
            cancellationToken: ct));

        var exists = await multi.ReadSingleAsync<bool>();
        if (!exists)
        {
            return null;
        }

        var rows = (await multi.ReadAsync<PhaseIntervalSqlRow>())
            .Select(r => new PhaseIntervalBo
            {
                ReportId = r.report_id,
                Phase = r.phase,
                EnteredAt = r.entered_at,
                ExitedAt = r.exited_at,
                RegressionCycleIndex = r.regression_cycle_index,
            })
            .ToArray();

        return rows;
    }

    public async Task<BugsByStatusBo> GetBugsByStatusAsync(int reportId, CancellationToken ct)
    {
        const string sql = @"
SELECT
    COUNT(*) FILTER (WHERE status = @open)::int      AS open,
    COUNT(*) FILTER (WHERE status = @fixedSt)::int   AS fixed,
    COUNT(*) FILTER (WHERE status = @verified)::int  AS verified,
    COUNT(*) FILTER (WHERE status = @rejected)::int  AS rejected
FROM public.bugs
WHERE report_id = @reportId;";

        await using var conn = await DataSource.OpenConnectionAsync(ct);
        var row = await conn.QuerySingleAsync<BugsByStatusSqlRow>(new CommandDefinition(
            sql,
            new
            {
                reportId,
                open = (int)BugStatus.Open,
                fixedSt = (int)BugStatus.Fixed,
                verified = (int)BugStatus.Verified,
                rejected = (int)BugStatus.Rejected,
            },
            cancellationToken: ct));

        return new BugsByStatusBo
        {
            Open = row.open,
            Fixed = row.@fixed,
            Verified = row.verified,
            Rejected = row.rejected,
        };
    }

    public async Task<int> GetBugsAddedDuringRegressionAsync(int reportId, CancellationToken ct)
    {
        // Баг считается добавленным во время регрессии, если его created_at
        // попадает хотя бы в один Test-интервал с regression_cycle_index >= 1.
        const string sql = @"
SELECT COUNT(DISTINCT b.id)::int
FROM public.bugs b
JOIN public.report_phase_intervals rpi
    ON rpi.report_id = b.report_id
   AND rpi.phase = @phaseTest
   AND rpi.regression_cycle_index >= 1
   AND b.created_at >= rpi.entered_at
   AND (rpi.exited_at IS NULL OR b.created_at < rpi.exited_at)
WHERE b.report_id = @reportId;";

        await using var conn = await DataSource.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { reportId, phaseTest = PhaseTest },
            cancellationToken: ct));
    }

    // Три result set'а для responsible-summary:
    // (1) participated — активные репорты с интервалом, пересекающимся с окном
    //     [from; to). Пересечение: entered_at < @to AND (exited_at IS NULL OR exited_at >= @from).
    // (2) completed — репорты в Resolved/Rejected с MAX(exited_at) ∈ окне.
    // (3) avg_fix_phase_days — среднее duration Fix-интервалов в днях среди completed.
    // Limit 10 на каждый список.
    private const string ResponsibleDataSql = @"
-- (1) participated
WITH user_reports AS (
    SELECT r.id, r.title, r.status
    FROM public.reports r
    WHERE r.creator_organization_id = @workspaceId
      AND r.is_excluded_from_analytics = FALSE
      AND r.id IN (SELECT rp.report_id FROM public.report_participants rp WHERE rp.user_id = @userId)
), active AS (
    SELECT ur.id, ur.title, ur.status
    FROM user_reports ur
    WHERE ur.status NOT IN (@statusResolved, @statusRejected)
      AND EXISTS (
          SELECT 1
          FROM public.report_phase_intervals rpi
          WHERE rpi.report_id = ur.id
            AND rpi.entered_at < @to
            AND (rpi.exited_at IS NULL OR rpi.exited_at >= @from)
      )
)
SELECT id AS report_id, title, status
FROM active
ORDER BY id DESC
LIMIT 10;

-- (2) completed
WITH user_reports AS (
    SELECT r.id, r.title, r.status
    FROM public.reports r
    WHERE r.creator_organization_id = @workspaceId
      AND r.is_excluded_from_analytics = FALSE
      AND r.status IN (@statusResolved, @statusRejected)
      AND r.id IN (SELECT rp.report_id FROM public.report_participants rp WHERE rp.user_id = @userId)
), closed AS (
    SELECT ur.id, ur.title, ur.status, MAX(rpi.exited_at) AS closed_at
    FROM user_reports ur
    JOIN public.report_phase_intervals rpi ON rpi.report_id = ur.id
    GROUP BY ur.id, ur.title, ur.status
    HAVING MAX(rpi.exited_at) >= @from AND MAX(rpi.exited_at) < @to
)
SELECT id AS report_id, title, status, closed_at
FROM closed
ORDER BY closed_at DESC, id DESC
LIMIT 10;

-- (3) avg_fix_phase_days — среднее duration Fix-интервалов в днях среди
-- completed-репортов (тот же фильтр + закрытый интервал).
WITH user_reports AS (
    SELECT r.id
    FROM public.reports r
    WHERE r.creator_organization_id = @workspaceId
      AND r.is_excluded_from_analytics = FALSE
      AND r.status IN (@statusResolved, @statusRejected)
      AND r.id IN (SELECT rp.report_id FROM public.report_participants rp WHERE rp.user_id = @userId)
), closed AS (
    SELECT ur.id
    FROM user_reports ur
    JOIN public.report_phase_intervals rpi ON rpi.report_id = ur.id
    GROUP BY ur.id
    HAVING MAX(rpi.exited_at) >= @from AND MAX(rpi.exited_at) < @to
), fix_intervals AS (
    SELECT rpi.report_id, SUM(rpi.duration_seconds)::bigint AS fix_seconds
    FROM public.report_phase_intervals rpi
    JOIN closed c ON c.id = rpi.report_id
    WHERE rpi.phase = @phaseFix
      AND rpi.exited_at IS NOT NULL
    GROUP BY rpi.report_id
)
SELECT
    CASE
        WHEN COUNT(*) = 0 THEN NULL
        ELSE AVG(fix_seconds) / 86400.0
    END AS avg_fix_phase_days
FROM fix_intervals;
";

    public async Task<AnalyticsResponsibleRawData> GetResponsibleDataAsync(
        string workspaceId,
        string userId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var parameters = new
        {
            workspaceId,
            userId,
            from,
            to,
            phaseFix = PhaseFix,
            statusResolved = StatusResolved,
            statusRejected = StatusRejected,
        };

        await using var conn = await DataSource.OpenConnectionAsync(ct);
        await using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            ResponsibleDataSql, parameters, cancellationToken: ct));

        var participated = (await multi.ReadAsync<ResponsibleParticipatedSqlRow>())
            .Select(r => new ResponsibleParticipatedReportBo
            {
                ReportId = r.report_id,
                Title = r.title ?? string.Empty,
                CurrentPhase = MapStatusToPhase(r.status),
            })
            .ToArray();

        var completed = (await multi.ReadAsync<ResponsibleCompletedSqlRow>())
            .Select(r => new ResponsibleCompletedReportBo
            {
                ReportId = r.report_id,
                Title = r.title ?? string.Empty,
                ClosedAt = r.closed_at,
                Outcome = (short)r.status,
            })
            .ToArray();

        var avg = await multi.ReadFirstOrDefaultAsync<double?>();

        return new AnalyticsResponsibleRawData
        {
            Participated = participated,
            Completed = completed,
            AvgFixPhaseDays = avg,
        };
    }

    /// <summary>
    /// Активные репорты живут либо в Test, либо в Fix; контрактный
    /// <c>PhaseName</c> ограничен теми же двумя значениями.
    /// </summary>
    private static short MapStatusToPhase(int status)
    {
        if (status == PhaseFix)
        {
            return PhaseFix;
        }
        return PhaseTest;
    }

    private sealed class ResponsibleParticipatedSqlRow
    {
        public int report_id { get; init; }
        public string? title { get; init; }
        public int status { get; init; }
    }

    private sealed class ResponsibleCompletedSqlRow
    {
        public int report_id { get; init; }
        public string? title { get; init; }
        public int status { get; init; }
        public DateTimeOffset closed_at { get; init; }
    }

    private sealed class ClosedReportSqlRow
    {
        public int report_id { get; init; }
        public string? title { get; init; }
        public DateTimeOffset created_at { get; init; }
        public DateTimeOffset closed_at { get; init; }
        public DateTimeOffset? first_test_entered_at { get; init; }
        public int test_intervals { get; init; }
        public int fix_intervals { get; init; }
        public long test_duration_seconds { get; init; }
        public long fix_duration_seconds { get; init; }
    }

    private sealed class PhaseAggregateSqlRow
    {
        public int bucket { get; init; }
        public int report_count { get; init; }
        public long total_duration_seconds { get; init; }
    }

    private sealed class PhaseTrendSqlRow
    {
        public string iso_week { get; init; } = string.Empty;
        public double test_days { get; init; }
        public double fix_days { get; init; }
        public int reports_closed { get; init; }
    }

    private sealed class PhaseIntervalSqlRow
    {
        public int report_id { get; init; }
        public short phase { get; init; }
        public DateTimeOffset entered_at { get; init; }
        public DateTimeOffset? exited_at { get; init; }
        public int regression_cycle_index { get; init; }
    }

    private sealed class BugsByStatusSqlRow
    {
        public int open { get; init; }
        public int @fixed { get; init; }
        public int verified { get; init; }
        public int rejected { get; init; }
    }
}
