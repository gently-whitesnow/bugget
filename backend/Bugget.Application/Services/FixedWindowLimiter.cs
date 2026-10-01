using System.Collections.Concurrent;

namespace Bugget.Application.Services;

/// <summary>
/// «Не больше N событий на ключ за окно» — для неудачных PAT-попыток и write-инструментов агента.
/// Состояние процесса, а не БД: контур одноинстансный, после рестарта счёт начинается заново.
/// </summary>
public sealed class FixedWindowLimiter(TimeProvider timeProvider, int limit, TimeSpan window)
{
    /// <summary>Порог уборки отработавших окон: иначе словарь рос бы на каждый виденный ключ.</summary>
    private const int SweepThreshold = 1024;

    private readonly ConcurrentDictionary<string, Window> _windows = new();

    /// <summary>Учитывает событие и отвечает, в лимите ли ключ. Отказ событие не откатывает.</summary>
    public bool TryAcquire(string key) => Record(key) <= limit;

    /// <summary>Переполнено ли окно — без записи события: там, где считаются только неудачи.</summary>
    public bool IsLimited(string key)
    {
        var now = timeProvider.GetUtcNow();
        return _windows.TryGetValue(key, out var window)
            && window.ExpiresAt > now
            && window.Count >= limit;
    }

    /// <summary>Записывает событие и возвращает счёт текущего окна ключа.</summary>
    public int Record(string key)
    {
        var now = timeProvider.GetUtcNow();

        if (_windows.Count >= SweepThreshold)
        {
            SweepExpired(now);
        }

        while (true)
        {
            if (!_windows.TryGetValue(key, out var existing))
            {
                if (_windows.TryAdd(key, new Window(now.Add(window), 1)))
                {
                    return 1;
                }

                continue;
            }

            var next = existing.ExpiresAt <= now
                ? new Window(now.Add(window), 1)
                : existing with { Count = existing.Count + 1 };

            if (_windows.TryUpdate(key, next, existing))
            {
                return next.Count;
            }
        }
    }

    private void SweepExpired(DateTimeOffset now)
    {
        foreach (var (key, value) in _windows)
        {
            if (value.ExpiresAt <= now)
            {
                _windows.TryRemove(new KeyValuePair<string, Window>(key, value));
            }
        }
    }

    private readonly record struct Window(DateTimeOffset ExpiresAt, int Count);
}
