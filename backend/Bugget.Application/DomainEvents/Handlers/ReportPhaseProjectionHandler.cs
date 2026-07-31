using System.Globalization;
using System.Text.Json;
using Bugget.Application.Analytics;
using Bugget.Application.DomainEvents.Consumer;
using Bugget.Application.Ports;
using Bugget.Domain.Analytics;
using Bugget.Domain.DomainEvents;
using Bugget.Domain.Reports;
using Microsoft.Extensions.Logging;

namespace Bugget.Application.DomainEvents.Handlers;

/// <summary>
/// Projection-handler для read-model <c>report_phase_intervals</c>. Слушает
/// <c>bugget.report.status_changed</c>: закрывает активный интервал репорта (если есть),
/// для рабочих фаз (Test/Fix) открывает новый с <c>regression_cycle_index</c> = числу
/// уже закрытых интервалов той же фазы. INSERT идёт через
/// <c>ON CONFLICT (source_event_id) DO NOTHING</c> — основной механизм идемпотентности
/// при at-least-once доставке.
/// </summary>
public sealed class ReportPhaseProjectionHandler(
    IReportPhaseIntervalsDbClient intervalsClient,
    ILogger<ReportPhaseProjectionHandler> logger) : IDomainEventHandler
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
    };

    public string EventType => BuggetEventTypes.ReportStatusChanged;

    public async Task HandleAsync(
        DomainEvent evt,
        ITransactionScope scope,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ReportStatusChangedPayload>(evt.Payload, PayloadJsonOptions)
            ?? throw new InvalidOperationException(
                $"ReportStatusChangedPayload is null for event_id={evt.Id}");

        var fromStatus = ParseStatus(payload.FromStatus, nameof(payload.FromStatus), evt.Id);
        var toStatus = ParseStatus(payload.ToStatus, nameof(payload.ToStatus), evt.Id);

        var reportId = int.Parse(evt.AggregateId, CultureInfo.InvariantCulture);

        // `from == to` отфильтровано в `ReportStatusEventFactory`, но защищаемся: активный
        // интервал той же фазы трогать не нужно.
        if (fromStatus != toStatus)
        {
            var closed = await intervalsClient.CloseActiveIntervalAsync(
                scope, reportId, evt.OccurredAt, evt.Id, ct);

            if (closed > 0)
            {
                logger.LogDebug(
                    "report_phase_intervals closed active interval: report_id={ReportId} exited_at={ExitedAt:o} event_id={EventId}",
                    reportId, evt.OccurredAt, evt.Id);
            }
        }

        if (toStatus is ReportStatus.Test or ReportStatus.Fix)
        {
            var phase = (short)toStatus;
            var regressionCycleIndex = await intervalsClient.CountClosedIntervalsAsync(
                scope, reportId, phase, ct);

            var inserted = await intervalsClient.InsertIntervalAsync(
                scope,
                new OpenReportPhaseIntervalCommand
                {
                    ReportId = reportId,
                    Phase = phase,
                    EnteredAt = evt.OccurredAt,
                    RegressionCycleIndex = regressionCycleIndex,
                    SourceEventId = evt.Id,
                },
                ct);

            if (inserted == 0)
            {
                // Повторная доставка того же события — UNIQUE(source_event_id) отработал.
                logger.LogDebug(
                    "report_phase_intervals duplicate source_event_id, skipping insert: report_id={ReportId} phase={Phase} event_id={EventId}",
                    reportId, phase, evt.Id);
            }
            else
            {
                logger.LogInformation(
                    "report_phase_intervals opened: report_id={ReportId} phase={Phase} regression_cycle_index={Idx} event_id={EventId}",
                    reportId, phase, regressionCycleIndex, evt.Id);
            }
        }
        else
        {
            logger.LogDebug(
                "report_phase_intervals terminal transition (no insert): report_id={ReportId} to_status={ToStatus} event_id={EventId}",
                reportId, toStatus, evt.Id);
        }
    }

    private static ReportStatus ParseStatus(string raw, string fieldName, long eventId)
    {
        if (Enum.TryParse<ReportStatus>(raw, ignoreCase: false, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"Unknown ReportStatus '{raw}' in {fieldName} for event_id={eventId}");
    }
}
