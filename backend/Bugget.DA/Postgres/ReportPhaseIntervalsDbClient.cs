using Bugget.BO.Ports;
using Bugget.DA.Transactions;
using Bugget.Entities.BO.Analytics;
using Dapper;

namespace Bugget.DA.Postgres;

public sealed class ReportPhaseIntervalsDbClient : PostgresClient, IReportPhaseIntervalsDbClient
{
    public async Task<int> CountClosedIntervalsAsync(
        ITransactionScope scope,
        int reportId,
        short phase,
        CancellationToken ct)
    {
        const string sql = @"
SELECT COUNT(*)::int
FROM public.report_phase_intervals
WHERE report_id = @reportId
  AND phase = @phase
  AND exited_at IS NOT NULL;";

        var (connection, transaction) = scope.Unwrap();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { reportId, phase },
            transaction: transaction,
            cancellationToken: ct));
    }

    public async Task<int> CloseActiveIntervalAsync(
        ITransactionScope scope,
        int reportId,
        DateTimeOffset exitedAt,
        long currentEventId,
        CancellationToken ct)
    {
        // Идемпотентность при replay'ах:
        // 1. `entered_at < @exitedAt` — replay более раннего события не должен закрыть
        //    активный интервал, открытый более поздним событием (inversion).
        // 2. `source_event_id <> @currentEventId` — replay того же события, что открыло
        //    этот активный интервал, не должен его закрывать собой же (self-close).
        const string sql = @"
UPDATE public.report_phase_intervals
SET exited_at = @exitedAt
WHERE report_id = @reportId
  AND exited_at IS NULL
  AND entered_at < @exitedAt
  AND source_event_id <> @currentEventId;";

        var (connection, transaction) = scope.Unwrap();
        return await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { reportId, exitedAt, currentEventId },
            transaction: transaction,
            cancellationToken: ct));
    }

    public async Task<int> InsertIntervalAsync(
        ITransactionScope scope,
        ReportPhaseIntervalInsert row,
        CancellationToken ct)
    {
        const string sql = @"
INSERT INTO public.report_phase_intervals
    (report_id, phase, entered_at, regression_cycle_index, source_event_id)
VALUES
    (@ReportId, @Phase, @EnteredAt, @RegressionCycleIndex, @SourceEventId)
ON CONFLICT (source_event_id) DO NOTHING;";

        var (connection, transaction) = scope.Unwrap();
        return await connection.ExecuteAsync(new CommandDefinition(
            sql,
            row,
            transaction: transaction,
            cancellationToken: ct));
    }
}
