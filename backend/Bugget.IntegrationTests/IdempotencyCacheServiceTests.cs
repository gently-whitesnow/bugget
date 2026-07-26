using Bugget.BO.Services.Idempotency;
using Bugget.DA.Interfaces;
using Bugget.IntegrationTests.Fixtures;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class IdempotencyCacheServiceTests : IClassFixture<AppWithPostgresFixture>
{
    private readonly IdempotencyCacheService _service;
    private readonly IIdempotencyCacheDbClient _db;
    private readonly string _connectionString;

    public IdempotencyCacheServiceTests(AppWithPostgresFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        _service = scope.ServiceProvider.GetRequiredService<IdempotencyCacheService>();
        _db = scope.ServiceProvider.GetRequiredService<IIdempotencyCacheDbClient>();
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
    }

    [Fact(DisplayName = "Первый вызов выполняет factory, повторный возвращает cached без factory")]
    public async Task GetOrCompute_RepeatCall_ReturnsCachedWithoutFactory()
    {
        var key = $"k_{Guid.NewGuid()}";
        var calls = 0;

        var first = await _service.GetOrComputeAsync(
            key,
            _ => { calls++; return Task.FromResult(new Payload(42, "hello")); },
            TimeSpan.FromHours(24));

        var second = await _service.GetOrComputeAsync(
            key,
            _ => { calls++; return Task.FromResult(new Payload(-1, "should not run")); },
            TimeSpan.FromHours(24));

        Assert.Equal(1, calls);
        Assert.Equal(first, second);
        Assert.Equal(42, second.ReportId);
        Assert.Equal("hello", second.Title);
    }

    [Fact(DisplayName = "Expired entries удаляются DeleteExpiredAsync")]
    public async Task DeleteExpired_RemovesExpiredEntries()
    {
        var key = $"k_{Guid.NewGuid()}";

        await _db.InsertAsync(key, "{\"x\":1}", DateTimeOffset.UtcNow.AddMinutes(-5));

        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var before = await conn.ExecuteScalarAsync<long>(
                "SELECT count(*) FROM public.idempotency_cache WHERE key=@k", new { k = key });
            Assert.Equal(1, before);
        }

        await _db.DeleteExpiredAsync();

        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var after = await conn.ExecuteScalarAsync<long>(
                "SELECT count(*) FROM public.idempotency_cache WHERE key=@k", new { k = key });
            Assert.Equal(0, after);
        }
    }

    [Fact(DisplayName = "Expired entry не возвращается TryGetAsync")]
    public async Task TryGet_DoesNotReturnExpiredEntry()
    {
        var key = $"k_{Guid.NewGuid()}";
        await _db.InsertAsync(key, "{\"x\":1}", DateTimeOffset.UtcNow.AddSeconds(-1));

        var result = await _db.TryGetAsync(key);

        Assert.Null(result);
    }

    private sealed record Payload(int ReportId, string Title);
}
