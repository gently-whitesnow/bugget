using Bugget.DA.Interfaces;
using Bugget.DA.Transactions;
using Dapper;

namespace Bugget.DA.Postgres;

public sealed class IdempotencyCacheDbClient : PostgresClient, IIdempotencyCacheDbClient
{
    public async Task<string?> TryGetAsync(string key, CancellationToken ct = default)
    {
        const string sql = @"
SELECT response_json::text
FROM public.idempotency_cache
WHERE key = @key AND expires_at > now();";

        await using var conn = await DataSource.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            sql, new { key }, cancellationToken: ct));
    }

    /// <summary>
    /// Чтение кэшированного ответа в рамках транзакции.
    /// Используется Internal*-сервисами после <see cref="AcquireLockInternalAsync"/>.
    /// </summary>
    public Task<string?> TryGetAsync(ITransactionScope scope, string key, CancellationToken ct = default)
    {
        const string sql = @"
SELECT response_json::text
FROM public.idempotency_cache
WHERE key = @key AND expires_at > now();";

        var (connection, tx) = scope.Unwrap();
        return connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            sql, new { key }, transaction: tx, cancellationToken: ct));
    }

    public async Task InsertAsync(string key, string responseJson, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO public.idempotency_cache (key, response_json, expires_at)
VALUES (@key, @json::jsonb, @expires_at)
ON CONFLICT (key) DO NOTHING;";

        await using var conn = await DataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            sql, new { key, json = responseJson, expires_at = expiresAt }, cancellationToken: ct));
    }

    /// <summary>
    /// Атомарный upsert кэшированного ответа в рамках транзакции (DO UPDATE).
    /// В отличие от <see cref="InsertAsync"/>, перезаписывает запись — это нужно когда caller
    /// удерживает <c>pg_advisory_xact_lock</c> и точно знает, что предыдущая запись истёкла или отсутствует.
    /// </summary>
    public Task UpsertAsync(
        ITransactionScope scope,
        string key,
        string responseJson,
        DateTimeOffset expiresAt,
        CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO public.idempotency_cache (key, response_json, expires_at)
VALUES (@key, @json::jsonb, @expires_at)
ON CONFLICT (key) DO UPDATE
SET response_json = EXCLUDED.response_json,
    expires_at    = EXCLUDED.expires_at;";

        var (connection, tx) = scope.Unwrap();
        return connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { key, json = responseJson, expires_at = expiresAt },
            transaction: tx,
            cancellationToken: ct));
    }

    /// <summary>
    /// Транзакционный advisory-lock по ключу идемпотентности (auto-release on commit/rollback).
    /// Сериализует параллельные replay'и одного и того же idempotency-key.
    /// </summary>
    public Task AcquireLockInternalAsync(
        ITransactionScope scope,
        string key,
        CancellationToken ct = default)
    {
        const string sql = "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0));";

        var (connection, tx) = scope.Unwrap();
        return connection.ExecuteAsync(new CommandDefinition(
            sql, new { key }, transaction: tx, cancellationToken: ct));
    }

    public async Task<int> DeleteExpiredAsync(CancellationToken ct = default)
    {
        const string sql = "DELETE FROM public.idempotency_cache WHERE expires_at <= now();";

        await using var conn = await DataSource.OpenConnectionAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }
}
