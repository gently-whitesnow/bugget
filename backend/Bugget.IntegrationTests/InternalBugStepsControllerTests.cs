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
public class InternalBugStepsControllerTests : IClassFixture<AppWithPostgresFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _client;
    private readonly string _connectionString;

    public InternalBugStepsControllerTests(AppWithPostgresFixture fixture)
    {
        _client = fixture.CreateClient();
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
    }

    [Fact(DisplayName = "POST .../steps: создаёт шаг и возвращает stepId+stepNumber")]
    public async Task Create_PersistsStep()
    {
        var (bugId, testerId) = await CreateBugAsync(_client);

        var resp = await _client.SendAsync(BuildPost(bugId, new InternalCreateBugStepRequestDto
        {
            CreatorUserId = testerId,
            Text = "1. Открыть приложение и нажать кнопку",
            StepNumber = 1,
        }, idempotencyKey: Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<InternalCreateBugStepResponseDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.True(body!.StepId > 0);
        Assert.Equal(1, body.StepNumber);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var row = await conn.QuerySingleAsync<(int bug_id, string text, string creator_user_id, int step_number)>(
            "SELECT bug_id, text, creator_user_id, step_number FROM public.bug_steps WHERE id = @id",
            new { id = body.StepId });
        Assert.Equal(bugId, row.bug_id);
        Assert.Equal("1. Открыть приложение и нажать кнопку", row.text);
        Assert.Equal(testerId, row.creator_user_id);
        Assert.Equal(1, row.step_number);
    }

    [Fact(DisplayName = "Retry с тем же Idempotency-Key возвращает тот же stepId — без второй вставки")]
    public async Task Retry_WithSameKey_ReturnsSameStepId()
    {
        var (bugId, testerId) = await CreateBugAsync(_client);
        var key = Guid.NewGuid().ToString();
        var payload = new InternalCreateBugStepRequestDto
        {
            CreatorUserId = testerId,
            Text = "Повторный шаг — должен дедуплицироваться",
            StepNumber = 1,
        };

        var first = await _client.SendAsync(BuildPost(bugId, payload, idempotencyKey: key));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<InternalCreateBugStepResponseDto>(JsonOptions);

        var second = await _client.SendAsync(BuildPost(bugId, payload, idempotencyKey: key));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<InternalCreateBugStepResponseDto>(JsonOptions);

        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.Equal(firstBody!.StepId, secondBody!.StepId);
        Assert.Equal(firstBody.StepNumber, secondBody.StepNumber);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var stepCount = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM public.bug_steps WHERE bug_id = @b",
            new { b = bugId });
        Assert.Equal(1, stepCount);

        var cacheCount = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM public.idempotency_cache WHERE key = @k",
            new { k = key });
        Assert.Equal(1, cacheCount);
    }

    [Fact(DisplayName = "Отсутствует Idempotency-Key → 400")]
    public async Task Missing_IdempotencyKey_ReturnsBadRequest()
    {
        var (bugId, testerId) = await CreateBugAsync(_client);

        var req = new HttpRequestMessage(HttpMethod.Post, $"/v2/_internal/bugs/{bugId}/steps")
        {
            Content = JsonContent.Create(new InternalCreateBugStepRequestDto
            {
                CreatorUserId = testerId,
                Text = "Шаг без ключа",
                StepNumber = 1,
            }, options: JsonOptions),
        };
        req.Headers.Add("X-Client-Name", "beta-bot");

        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact(DisplayName = "Несуществующий bug → 404")]
    public async Task UnknownBug_ReturnsNotFound()
    {
        var resp = await _client.SendAsync(BuildPost(99_999_999, new InternalCreateBugStepRequestDto
        {
            CreatorUserId = $"tester_{Guid.NewGuid():N}",
            Text = "Шаг для несуществующего бага",
            StepNumber = 1,
        }, idempotencyKey: Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact(DisplayName = "Без X-Client-Name → 401")]
    public async Task Missing_ClientName_ReturnsUnauthorized()
    {
        var (bugId, testerId) = await CreateBugAsync(_client);

        var req = new HttpRequestMessage(HttpMethod.Post, $"/v2/_internal/bugs/{bugId}/steps")
        {
            Content = JsonContent.Create(new InternalCreateBugStepRequestDto
            {
                CreatorUserId = testerId,
                Text = "Шаг без X-Client-Name",
                StepNumber = 1,
            }, options: JsonOptions),
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    private static HttpRequestMessage BuildPost(int bugId, InternalCreateBugStepRequestDto payload, string idempotencyKey)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/v2/_internal/bugs/{bugId}/steps")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        req.Headers.Add("X-Client-Name", "beta-bot");
        req.Headers.Add("Idempotency-Key", idempotencyKey);
        return req;
    }

    private static async Task<(int bugId, string testerId)> CreateBugAsync(HttpClient client)
    {
        var testerId = $"tester_{Guid.NewGuid():N}";
        var payload = new InternalCreateBugRequestDto
        {
            WorkspaceId = $"ws_{Guid.NewGuid():N}",
            CreatorUserId = testerId,
            Receive = "Что-то пошло не так при запуске",
            Expect = "Должно было запуститься штатно",
        };
        var req = new HttpRequestMessage(HttpMethod.Post, "/v2/_internal/bugs")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        req.Headers.Add("X-Client-Name", "beta-bot");
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<InternalCreateBugResponseDto>(JsonOptions);
        return (body!.BugId, testerId);
    }
}
