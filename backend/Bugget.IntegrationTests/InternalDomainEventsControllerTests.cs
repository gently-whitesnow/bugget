using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bugget.Entities.DTO.Internal;
using Bugget.IntegrationTests.Fixtures;
using Dapper;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class InternalDomainEventsControllerTests : IClassFixture<AppWithPostgresFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _client;
    private readonly string _connectionString;

    public InternalDomainEventsControllerTests(AppWithPostgresFixture fixture)
    {
        _client = fixture.CreateClient();
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
    }

    [Fact(DisplayName = "GET /v2/_internal/domain-events: cursor-paging + event_types фильтр + ACL по workspace")]
    public async Task List_CursorPagingAndFilterAndAcl()
    {
        var workspaceId = $"ws_{Guid.NewGuid():N}";
        var otherWorkspace = $"ws_other_{Guid.NewGuid():N}";

        var id1 = await InsertEventAsync(workspaceId, "bug", "1", "bugget.bug.created");
        var id2 = await InsertEventAsync(workspaceId, "comment", "2", "bugget.comment.created");
        var id3 = await InsertEventAsync(workspaceId, "bug", "3", "bugget.bug.status_changed");
        await InsertEventAsync(otherWorkspace, "bug", "99", "bugget.bug.created");

        var first = await ListAsync(workspaceId, sinceId: 0, limit: 2);
        Assert.Equal(2, first!.Items.Count);
        Assert.Equal(id1, first.Items[0].Id);
        Assert.Equal(id2, first.Items[1].Id);
        Assert.Equal(id2, first.NextSinceId);

        var second = await ListAsync(workspaceId, sinceId: first.NextSinceId!.Value, limit: 2);
        Assert.Single(second!.Items);
        Assert.Equal(id3, second.Items[0].Id);
        Assert.Null(second.NextSinceId);

        var filtered = await ListAsync(workspaceId, sinceId: 0, limit: 100,
            eventTypes: "bugget.bug.created,bugget.bug.status_changed");
        Assert.Equal(2, filtered!.Items.Count);
        Assert.DoesNotContain(filtered.Items, i => i.EventType == "bugget.comment.created");

        Assert.DoesNotContain(first.Items, i => i.WorkspaceId == otherWorkspace);
    }

    [Fact(DisplayName = "GET /v2/_internal/domain-events/latest-id: max(id) или 0")]
    public async Task LatestId_MaxOrZero()
    {
        var empty = $"ws_empty_{Guid.NewGuid():N}";
        var populated = $"ws_pop_{Guid.NewGuid():N}";

        var body0 = await LatestIdAsync(empty);
        Assert.Equal(0L, body0!.LatestId);

        var id1 = await InsertEventAsync(populated, "bug", "1", "bugget.bug.created");
        var id2 = await InsertEventAsync(populated, "bug", "2", "bugget.bug.created");
        var bodyN = await LatestIdAsync(populated);
        Assert.Equal(Math.Max(id1, id2), bodyN!.LatestId);
    }

    [Fact(DisplayName = "GET без X-Client-Name → 401")]
    public async Task List_MissingClientName_Returns401()
    {
        var req = new HttpRequestMessage(
            HttpMethod.Get,
            "/v2/_internal/domain-events?workspaceId=ws_x&sinceId=0");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact(DisplayName = "GET без workspaceId → 400")]
    public async Task List_MissingWorkspaceId_Returns400()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/v2/_internal/domain-events?sinceId=0");
        req.Headers.Add("X-Client-Name", "beta-bot");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact(DisplayName = "GET latest-id без workspaceId → 400")]
    public async Task LatestId_MissingWorkspaceId_Returns400()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/v2/_internal/domain-events/latest-id");
        req.Headers.Add("X-Client-Name", "beta-bot");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    private async Task<InternalDomainEventsListResponseDto?> ListAsync(
        string workspaceId, long sinceId, int limit, string? eventTypes = null)
    {
        var url = $"/v2/_internal/domain-events?workspaceId={workspaceId}&sinceId={sinceId}&limit={limit}";
        if (eventTypes is not null)
        {
            url += $"&eventTypes={Uri.EscapeDataString(eventTypes)}";
        }

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Client-Name", "beta-bot");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return await resp.Content.ReadFromJsonAsync<InternalDomainEventsListResponseDto>(JsonOptions);
    }

    private async Task<InternalDomainEventLatestIdResponseDto?> LatestIdAsync(string workspaceId)
    {
        var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"/v2/_internal/domain-events/latest-id?workspaceId={workspaceId}");
        req.Headers.Add("X-Client-Name", "beta-bot");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return await resp.Content.ReadFromJsonAsync<InternalDomainEventLatestIdResponseDto>(JsonOptions);
    }

    private async Task<long> InsertEventAsync(
        string workspaceId, string aggregateType, string aggregateId, string eventType)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<long>(@"
INSERT INTO public.domain_events
    (workspace_id, aggregate_type, aggregate_id, event_type, event_version, payload)
VALUES
    (@workspaceId, @aggregateType, @aggregateId, @eventType, 1, '{""k"":""v""}'::jsonb)
RETURNING id;",
            new { workspaceId, aggregateType, aggregateId, eventType });
    }
}
