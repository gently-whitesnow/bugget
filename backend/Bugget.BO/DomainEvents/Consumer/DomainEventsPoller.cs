using Bugget.BO.Ports;
using Bugget.Entities.BO.DomainEvents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugget.BO.DomainEvents.Consumer;

/// <summary>
/// Polling-консьюмер `domain_events`: читает события по cursor, диспатчит в handler'ы по
/// EventType, двигает cursor в той же транзакции, что side-effects (per-event tx → at-least-once
/// с откатом на handler exception). Транзакция берётся портом <see cref="IUnitOfWork"/> —
/// драйвер БД в BO не виден.
/// </summary>
public sealed class DomainEventsPoller(
    IUnitOfWork unitOfWork,
    IDomainEventsCursorClient cursorClient,
    IDomainEventsDbClient eventsClient,
    IEnumerable<IDomainEventHandler> handlers,
    IOptions<DomainEventsConsumerOptions> options,
    ILogger<DomainEventsPoller> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    private readonly DomainEventsConsumerOptions _options = options.Value;
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    // Дубликат EventType → ArgumentException на старте (fail-fast).
    private readonly Dictionary<string, IDomainEventHandler> _handlers = handlers.ToDictionary(h => h.EventType);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "domain_events poller starting (consumer={ConsumerName}, poll={PollSec}s, batch={Batch}, handlers={HandlerCount}, event_types={EventTypes})",
            _options.ConsumerName,
            _options.PollingInterval.TotalSeconds,
            _options.BatchSize,
            _handlers.Count,
            string.Join(",", _handlers.Keys));

        while (!stoppingToken.IsCancellationRequested)
        {
            int processed;
            try
            {
                processed = await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "domain_events poller tick failed, backing off {BackoffSec}s", _options.ErrorBackoff.TotalSeconds);
                if (!await SafeDelayAsync(_options.ErrorBackoff, stoppingToken))
                {
                    return;
                }
                continue;
            }

            if (processed < _options.BatchSize)
            {
                if (!await SafeDelayAsync(_options.PollingInterval, stoppingToken))
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Один тик: читает batch событий по cursor'у и обрабатывает их per-event-tx.
    /// Возвращает число успешно обработанных событий (≤ <c>BatchSize</c>).
    /// Public для smoke-тестов: интеграционный тест вызывает напрямую,
    /// без запуска <see cref="ExecuteAsync"/>.
    /// </summary>
    public async Task<int> TickAsync(CancellationToken ct)
    {
        var cursor = await cursorClient.GetAsync(_options.ConsumerName, ct);
        if (cursor is null)
        {
            logger.LogDebug("domain_events cursor row missing for consumer={ConsumerName}; waiting for bootstrap", _options.ConsumerName);
            return 0;
        }

        var batch = await eventsClient.ListAllAsync(cursor.Value, _options.BatchSize, ct);
        if (batch.Count == 0)
        {
            return 0;
        }

        var processed = 0;
        var lagSeconds = (_time.GetUtcNow() - batch[0].OccurredAt).TotalSeconds;
        logger.LogDebug(
            "domain_events tick: cursor={Cursor} batch_size={Count} head_lag_sec={LagSec:F1}",
            cursor.Value, batch.Count, lagSeconds);

        foreach (var evt in batch)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await ProcessOneAsync(evt, ct);
                processed++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Handler упал — не двигаем cursor дальше по batch'у, иначе нарушим порядок:
                // событие переедет на следующем тике.
                logger.LogError(
                    ex,
                    "domain_events handler failed: event_id={EventId} event_type={EventType} aggregate={AggregateType}:{AggregateId}; stopping batch",
                    evt.Id, evt.EventType, evt.AggregateType, evt.AggregateId);
                break;
            }
        }

        if (processed > 0)
        {
            logger.LogInformation(
                "domain_events tick processed events: consumer={ConsumerName} count={Count} cursor→{LastId}",
                _options.ConsumerName, processed, batch[processed - 1].Id);
        }

        return processed;
    }

    private async Task ProcessOneAsync(DomainEvent evt, CancellationToken ct)
    {
        await unitOfWork.ExecuteAsync(async (scope, innerCt) =>
        {
            if (_handlers.TryGetValue(evt.EventType, out var handler))
            {
                await handler.HandleAsync(evt, scope, innerCt);
            }
            else
            {
                // Неизвестный тип — handler'а нет / обработает другой consumer; двигаем cursor дальше.
                logger.LogDebug(
                    "domain_events skipping (no handler): event_id={EventId} event_type={EventType}",
                    evt.Id, evt.EventType);
            }

            var updated = await cursorClient.UpdateAsync(_options.ConsumerName, evt.Id, scope, innerCt);
            if (updated == 0)
            {
                // Monotonic guard в UPDATE отклонил движение cursor'а назад. UNIQUE(source_event_id)
                // в read-model защищает от дублей, идём дальше.
                logger.LogWarning(
                    "domain_events cursor monotonic guard rejected update: event_id={EventId} consumer={ConsumerName}",
                    evt.Id, _options.ConsumerName);
            }
        }, ct);
    }

    private async Task<bool> SafeDelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, _time, ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
