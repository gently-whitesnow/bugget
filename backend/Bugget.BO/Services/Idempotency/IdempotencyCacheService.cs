using System.Text.Json;
using Bugget.DA.Interfaces;
using Bugget.DA.Transactions;
using Monade;

namespace Bugget.BO.Services.Idempotency;

public sealed class IdempotencyCacheService(IIdempotencyCacheDbClient db)
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<T> GetOrComputeAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        var cached = await db.TryGetAsync(key, ct);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<T>(cached, JsonOptions)
                   ?? throw new InvalidOperationException($"Idempotency cache entry for key '{key}' is null after deserialization.");
        }

        var value = await factory(ct);
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await db.InsertAsync(key, json, DateTimeOffset.UtcNow.Add(ttl), ct);
        return value;
    }

    /// <summary>
    /// In-transaction вариант: lock + try-get cached + factory + upsert внутри одной транзакции.
    /// Caller отвечает за коммит (UoW). При <see cref="MonadeStruct{T}.HasError"/> результат не кэшируется.
    /// Используется Internal*-сервисами, которые должны делать mutation+cache+event атомарно.
    /// </summary>
    public async Task<MonadeStruct<T>> GetOrComputeInScopeAsync<T>(
        ITransactionScope scope,
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<MonadeStruct<T>>> factory,
        CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        await db.AcquireLockInternalAsync(scope, key, ct);

        var cached = await db.TryGetAsync(scope, key, ct);
        if (cached is not null)
        {
            var deserialized = JsonSerializer.Deserialize<T>(cached, JsonOptions);
            if (deserialized is not null)
            {
                return deserialized;
            }
        }

        var result = await factory(ct);
        if (result.HasError)
        {
            return result;
        }

        var json = JsonSerializer.Serialize(result.Value, JsonOptions);
        await db.UpsertAsync(scope, key, json, DateTimeOffset.UtcNow.Add(ttl), ct);
        return result;
    }
}
