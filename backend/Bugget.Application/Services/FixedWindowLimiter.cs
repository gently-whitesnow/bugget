using System.Collections.Concurrent;

namespace Bugget.Application.Services;

/// <summary>
/// Счётчик «не больше N событий на ключ за окно» — общий для троттлинга неудачных
/// PAT-попыток и write-инструментов агента. Состояние процесса, а не БД: контур
/// одноинстансный (та же посылка, что у кулдауна fix-request), а после рестарта
/// счёт честно начинается заново.
/// </summary>
public sealed class FixedWindowLimiter(TimeProvider timeProvider, int limit, TimeSpan window)
{
    /// <summary>
    /// Порог уборки отработавших окон: без неё словарь рос бы на ключ за каждый
    /// когда-либо виденный субъект.
    /// </summary>
    private const int SweepThreshold = 1024;

    private readonly ConcurrentDictionary<string, Window> _windows = new();

    /// <summary>
    /// Учитывает событие и отвечает, укладывается ли ключ в лимит. Отказ события
    /// не откатывает: переполненное окно продолжает копить счёт.
    /// </summary>
    public bool TryAcquire(string key) => Record(key) <= limit;

    /// <summary>
    /// Переполнено ли окно ключа — без записи события. Нужно там, где считаются
    /// только неудачи: проверка стоит до работы, запись — после её провала.
    /// </summary>
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
