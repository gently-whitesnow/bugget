using Bugget.Domain.Bugs;
using Bugget.Domain.Reports;
using Dapper;
using Npgsql;

namespace Bugget.IntegrationTests;

/// <summary>Прямой посев закрытых репортов, интервалов фаз, багов и участников для analytics-тестов.</summary>
internal sealed class AnalyticsSeeder(string connectionString)
{
    public async Task<int> SeedClosedReportAsync(
        string workspaceId,
        string title,
        ReportStatus status,
        bool isExcluded,
        SeedInterval[] intervals,
        string? creatorTeamId = null)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var reportId = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO public.reports (
                title, status, responsible_user_id, creator_user_id,
                created_at, updated_at, creator_organization_id, is_excluded_from_analytics,
                creator_team_id
            ) VALUES (
                @title, @status, '', 'seed-user',
                now(), now(), @workspaceId, @isExcluded,
                @creatorTeamId
            ) RETURNING id;",
            new
            {
                title,
                status = (int)status,
                workspaceId,
                isExcluded,
                creatorTeamId,
            });

        // source_event_id должен быть уникальным глобально (UNIQUE constraint),
        // поэтому генерим псевдо-уникальный bigint от report_id + индекса.
        var seq = 0;
        foreach (var interval in intervals)
        {
            seq++;
            await conn.ExecuteAsync(@"
                INSERT INTO public.report_phase_intervals (
                    report_id, phase, entered_at, exited_at,
                    regression_cycle_index, source_event_id
                ) VALUES (
                    @reportId, @phase, @enteredAt, @exitedAt,
                    @regressionCycleIndex, @sourceEventId
                );",
                new
                {
                    reportId,
                    phase = (short)interval.Phase,
                    enteredAt = interval.EnteredAt,
                    exitedAt = interval.ExitedAt,
                    regressionCycleIndex = interval.RegressionCycleIndex,
                    sourceEventId = ((long)reportId * 1_000_000L) + seq,
                });
        }

        return reportId;
    }

    public async Task SeedBugAsync(int reportId, BugStatus status, DateTimeOffset createdAt)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO public.bugs (
                report_id, receive, expect, created_at, updated_at, creator_user_id, status
            ) VALUES (
                @reportId, 'r', 'e', @createdAt, @createdAt, 'seed-user', @status
            );",
            new { reportId, status = (int)status, createdAt });
    }

    public async Task SeedParticipantAsync(int reportId, string userId)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO public.report_participants (report_id, user_id)
            VALUES (@reportId, @userId)
            ON CONFLICT DO NOTHING;",
            new { reportId, userId });
    }
}
