using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Bugget.Infrastructure.Caching;

[PublicAPI]
public abstract class ReloadableMemoryCache<TKey, TValue> where TKey : notnull
{
    private int _initializedFlag;
    private readonly ILogger<ReloadableMemoryCache<TKey, TValue>> _logger;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheEntryOptions;

    private readonly bool _requiredToStartService;
    private readonly string _typeName;
    private readonly TimeSpan? _reloadInterval;
    private readonly TimeSpan _dueInterval = TimeSpan.FromSeconds(5);

    private Task? _reloadableTask;
    private CancellationTokenSource? _cts;
    private PeriodicTimer? _timer;

    /// <summary>
    /// Конструктор создания перезагружаемого локального кэша
    /// </summary>
    /// <remarks>Для IMemoryCache необходимо задать реализацию</remarks>
    /// <param name="logger"></param>
    /// <param name="cache"></param>
    /// <param name="cacheEntryOptions"></param>
    /// <param name="reloadInterval"></param>
    /// <param name="requiredToStartService"></param>
    protected ReloadableMemoryCache(ILogger<ReloadableMemoryCache<TKey, TValue>> logger,
        IMemoryCache cache,
        MemoryCacheEntryOptions cacheEntryOptions,
        TimeSpan? reloadInterval = null,
        bool requiredToStartService = false)
    {
        _logger = logger;
        _cache = cache;
        _reloadInterval = reloadInterval;
        _requiredToStartService = requiredToStartService;
        _cacheEntryOptions = cacheEntryOptions;

        _typeName = GetType().Name;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (Interlocked.CompareExchange(ref _initializedFlag, 1, 0) == 0)
        {
            await InitializeAsync(_cts.Token);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is null)
        {
            return;
        }

        _timer?.Dispose();

        await _cts.CancelAsync();
        _cts.Dispose();
        if (_reloadableTask == null)
        {
            return;
        }

        await _reloadableTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    public async Task<bool> TryReloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("{TypeName}.TryReloadAsync started", _typeName);
            var pairs = await GetDataAsync(cancellationToken);

            foreach (var (key, val) in pairs)
            {
                _cache.Set(key, val, _cacheEntryOptions);
            }

            _logger.LogInformation("{TypeName}.TryReloadAsync finished", _typeName);
            return true;
        }
        catch (TaskCanceledException exception)
        {
            _logger.LogInformation(exception, "Invoking {TypeName}.TryReloadAsync cancelled", _typeName);
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Invoking {TypeName}.TryReloadAsync failed", _typeName);
            return false;
        }
    }

    public bool TryGet(TKey key, out TValue? value)
    {
        var hasValue = _cache.TryGetValue<TValue>(key, out var valueFromCache);
        if (hasValue)
        {
            value = valueFromCache;
            return true;
        }

        value = default;
        return false;
    }

    public ICollection<TValue> GetMany(ICollection<TKey> keys)
    {
        var result = new List<TValue>(keys.Count);
        foreach (var key in keys)
        {
            if (_cache.TryGetValue<TValue>(key, out var value))
            {
                result.Add(value!);
            }
        }

        return result.ToArray();
    }

    protected abstract Task<IEnumerable<(TKey key, TValue val)>> GetDataAsync(CancellationToken cancellationToken);

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{TypeName}.InitializeAsync started", _typeName);

        if (await TryReloadAsync(cancellationToken))
        {
            if (_reloadInterval == null)
            {
                return;
            }
            _reloadableTask = Task.Run(() => RunReloadableTaskAsync(_reloadInterval.Value, cancellationToken), cancellationToken);
            _logger.LogInformation("{TypeName}.InitializeAsync finished", _typeName);
            return;
        }

        if (_requiredToStartService)
        {
            throw new InvalidOperationException($"{_typeName} required to start service, but initialize failed.");
        }

        _reloadableTask = Task.Run(async () =>
        {
            do
            {
                _logger.LogInformation("{TypeName}.InitializeAsync returned false. Trying it again.", _typeName);
                await Task.Delay(_dueInterval, cancellationToken);
            } while (await TryReloadAsync(cancellationToken) == false);

            _logger.LogInformation("{TypeName}.InitializeAsync finished", _typeName);

            if (_reloadInterval == null)
            {
                return;
            }

            await RunReloadableTaskAsync(_reloadInterval.Value, cancellationToken);
        }, cancellationToken);
    }

    private async Task RunReloadableTaskAsync(TimeSpan reloadInterval, CancellationToken cancellationToken)
    {
        _timer = new PeriodicTimer(reloadInterval);

        while (!cancellationToken.IsCancellationRequested && await _timer.WaitForNextTickAsync(cancellationToken))
        {
            await TryReloadAsync(cancellationToken);
        }
    }
}
