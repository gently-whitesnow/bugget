using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bugget.Entities.BO;
using Bugget.Entities.BO.Common;
using Bugget.Entities.DTO.Internal;
using Bugget.Entities.Views.Attachment;
using Bugget.IntegrationTests.Fixtures;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Bugget.IntegrationTests;

[Collection("PostgresCollection")]
public class InternalAttachmentsControllerTests : IClassFixture<AppWithPostgresFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    // Минимальный валидный PNG: 8-байтовая сигнатура + IHDR + IEND — достаточно,
    // чтобы libmagic уверенно распознал image/png.
    private static readonly byte[] PngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
        0x54, 0x08, 0x99, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82,
    ];

    // Минимальный валидный 1x1 GIF89a — тип `image/gif` есть в `AllowedMimes`,
    // но НЕ в whitelist'ах оптимизатора, поэтому байты сохраняются как есть.
    private static readonly byte[] GifBytes =
    [
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00,
        0x01, 0x00, 0x80, 0x00, 0x00, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0x00, 0x21, 0xF9, 0x04, 0x01, 0x00,
        0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44,
        0x01, 0x00, 0x3B,
    ];

    private readonly HttpClient _client;
    private readonly string _connectionString;
    private readonly FakeReportPageHubClient _hub;
    private readonly PostgresContainerFixture _postgres;

    public InternalAttachmentsControllerTests(AppWithPostgresFixture fixture, PostgresContainerFixture postgres)
    {
        _client = fixture.CreateClient();
        _connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!;
        _hub = fixture.Services.GetRequiredService<FakeReportPageHubClient>();
        _postgres = postgres;
    }

    [Fact(DisplayName = "POST /v2/_internal/attachments?bugId: валидный png → 201 + row в БД")]
    public async Task Upload_ValidPng_CreatesAttachment()
    {
        var (_, bugId, _) = await CreateBugAsync();

        var response = await UploadByBugAsync(bugId, "shot.png", "image/png", PngBytes, clientName: "beta-bot");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var view = await response.Content.ReadFromJsonAsync<AttachmentView>(JsonOptions);
        Assert.NotNull(view);
        Assert.Equal(bugId, view!.EntityId);
        Assert.Equal("shot.png", view.FileName);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var count = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM public.attachments WHERE entity_id = @bid AND attach_type = @at",
            new { bid = bugId, at = (int)AttachType.Fact });
        Assert.Equal(1, count);
    }

    [Fact(DisplayName = "11-й файл → attachment_limit_exceeded, первые 10 сохранены")]
    public async Task Upload_EleventhFile_RejectsButFirstTenSaved()
    {
        var (_, bugId, _) = await CreateBugAsync();

        for (var i = 0; i < 10; i++)
        {
            var ok = await UploadByBugAsync(bugId, $"ok-{i}.png", "image/png", PngBytes, clientName: "beta-bot");
            Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        }

        var eleventh = await UploadByBugAsync(bugId, "eleventh.png", "image/png", PngBytes, clientName: "beta-bot");
        Assert.Equal(HttpStatusCode.BadRequest, eleventh.StatusCode);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var count = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM public.attachments WHERE entity_id = @bid AND attach_type = @at",
            new { bid = bugId, at = (int)AttachType.Fact });
        Assert.Equal(10, count);
    }

    [Fact(DisplayName = "Неподдерживаемый MIME → 400")]
    public async Task Upload_UnsupportedMime_ReturnsBadRequest()
    {
        var (_, bugId, _) = await CreateBugAsync();

        // `.exe` с ELF/PE payload'ом не в AllowedMimes → reject.
        var bytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };
        var response = await UploadByBugAsync(bugId, "evil.exe", "application/octet-stream", bytes, clientName: "beta-bot");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "Отсутствует X-Client-Name → 401")]
    public async Task Upload_MissingClientName_ReturnsUnauthorized()
    {
        var (_, bugId, _) = await CreateBugAsync();

        var response = await UploadByBugAsync(bugId, "shot.png", "image/png", PngBytes, clientName: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "POST ?commentId: валидный png → 201 + row в БД с attach_type=Comment")]
    public async Task Upload_ToComment_CreatesCommentAttachment()
    {
        var (_, bugId, testerId) = await CreateBugAsync();
        var commentId = await CreateExternalCommentAsync(bugId, testerId, "комментарий тестера");

        var response = await UploadByCommentAsync(commentId, "shot.png", "image/png", PngBytes, clientName: "beta-bot");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var view = await response.Content.ReadFromJsonAsync<AttachmentView>(JsonOptions);
        Assert.NotNull(view);
        Assert.Equal(commentId, view!.EntityId);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var row = await conn.QuerySingleAsync<(long entity_id, short attach_type)>(
            "SELECT entity_id, attach_type FROM public.attachments WHERE id = @id",
            new { id = view.Id });
        Assert.Equal(commentId, row.entity_id);
        Assert.Equal((short)AttachType.Comment, row.attach_type);
    }

    [Fact(DisplayName = "POST ?commentId: публикует bugget.attachment.created с metadata comment attachment")]
    public async Task Upload_ToComment_PublishesAttachmentCreatedDomainEvent()
    {
        var (_, bugId, testerId) = await CreateBugAsync();
        var commentId = await CreateExternalCommentAsync(bugId, testerId, "comment with attachment");

        var response = await UploadByCommentAsync(commentId, "shot.png", "image/png", PngBytes, clientName: "beta-bot");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var view = await response.Content.ReadFromJsonAsync<AttachmentView>(JsonOptions);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var payload = await conn.ExecuteScalarAsync<string>(
            @"SELECT payload::text
              FROM public.domain_events
              WHERE event_type = 'bugget.attachment.created' AND aggregate_id = @aggregateId
              ORDER BY id DESC
              LIMIT 1;",
            new { aggregateId = view!.Id.ToString() });

        Assert.NotNull(payload);
        using var payloadJson = JsonDocument.Parse(payload);
        var payloadRoot = payloadJson.RootElement;
        Assert.Equal(view!.Id, payloadRoot.GetProperty("attachmentId").GetInt32());
        Assert.Equal(commentId, payloadRoot.GetProperty("commentId").GetInt32());
        Assert.Equal(bugId, payloadRoot.GetProperty("bugId").GetInt32());
        Assert.Equal((int)AttachType.Comment, payloadRoot.GetProperty("attachType").GetInt32());
        Assert.Equal("image/png", payloadRoot.GetProperty("contentType").GetString());
    }

    [Fact(DisplayName = "POST ?commentId: пушит SignalR SendAttachmentCreate с groupKey по reportId")]
    public async Task Upload_ToComment_PushesSignalRAttachmentCreate()
    {
        var (reportId, bugId, testerId) = await CreateBugAsync();
        var commentId = await CreateExternalCommentAsync(bugId, testerId, "comment with attach");

        var response = await UploadByCommentAsync(commentId, "shot.png", "image/png", PngBytes, clientName: "beta-bot");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var view = await response.Content.ReadFromJsonAsync<AttachmentView>(JsonOptions);

        var pushed = _hub.AttachmentCreates.SingleOrDefault(x => x.Attachment.Id == view!.Id);
        Assert.NotEqual(default, pushed);
        Assert.Equal(commentId, pushed.Attachment.EntityId);
        Assert.Equal((int)AttachType.Comment, pushed.Attachment.AttachType);
        // Default AliasMode → aliasId = raw reportId.ToString().
        Assert.EndsWith(reportId.ToString(), pushed.GroupKey);
    }

    [Fact(DisplayName = "POST ?commentId: SignalR groupKey содержит public_id при AliasMode=guid")]
    public async Task Upload_ToComment_GuidAliasMode_PushesPublicIdGroup()
    {
        await using var guidFactory = new AppWithPostgresFixture(_postgres);
        guidFactory.AliasModeOverride = "guid";
        var client = guidFactory.CreateClient();
        var hub = guidFactory.Services.GetRequiredService<FakeReportPageHubClient>();

        var (reportId, bugId, testerId) = await CreateBugAsync(client);
        var commentId = await CreateExternalCommentAsync(client, bugId, testerId, "guid mode comment");

        var response = await UploadByCommentAsync(client, commentId, "shot.png", "image/png", PngBytes, clientName: "beta-bot");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var view = await response.Content.ReadFromJsonAsync<AttachmentView>(JsonOptions);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var publicId = await conn.QuerySingleAsync<Guid>(
            "SELECT public_id FROM public.reports WHERE id = @id;",
            new { id = reportId });

        var pushed = hub.AttachmentCreates.SingleOrDefault(x => x.Attachment.Id == view!.Id);
        Assert.NotEqual(default, pushed);
        Assert.EndsWith(publicId.ToString(), pushed.GroupKey);
    }

    [Fact(DisplayName = "POST одновременно bugId и commentId → 400")]
    public async Task Upload_BothTargets_ReturnsBadRequest()
    {
        var (_, bugId, testerId) = await CreateBugAsync();
        var commentId = await CreateExternalCommentAsync(bugId, testerId, "comment");

        var multipart = BuildMultipart("shot.png", "image/png", PngBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/v2/_internal/attachments?bugId={bugId}&commentId={commentId}")
        {
            Content = multipart,
        };
        req.Headers.Add("X-Client-Name", "beta-bot");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact(DisplayName = "POST без bugId и без commentId → 400")]
    public async Task Upload_NeitherTarget_ReturnsBadRequest()
    {
        var multipart = BuildMultipart("shot.png", "image/png", PngBytes);
        var req = new HttpRequestMessage(HttpMethod.Post, "/v2/_internal/attachments")
        {
            Content = multipart,
        };
        req.Headers.Add("X-Client-Name", "beta-bot");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact(DisplayName = "POST ?commentId на несуществующий comment → 404")]
    public async Task Upload_UnknownComment_ReturnsNotFound()
    {
        var resp = await UploadByCommentAsync(99999999, "shot.png", "image/png", PngBytes, clientName: "beta-bot");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact(DisplayName = "GET /v2/_internal/attachments/{id}/content → 200 + bytes match")]
    public async Task Download_HappyPath_ReturnsBytes()
    {
        var (_, bugId, _) = await CreateBugAsync();

        // GIF не попадает в AttachmentOptimizator.CanOptimize, поэтому байты сохранятся
        // как есть и сравнение download↔upload будет валидным.
        var upload = await UploadByBugAsync(bugId, "shot.gif", "image/gif", GifBytes, clientName: "beta-bot");
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var view = await upload.Content.ReadFromJsonAsync<AttachmentView>(JsonOptions);

        var req = new HttpRequestMessage(HttpMethod.Get, $"/v2/_internal/attachments/{view!.Id}/content");
        req.Headers.Add("X-Client-Name", "beta-bot");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("image/gif", resp.Content.Headers.ContentType?.MediaType);

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.Equal(GifBytes, bytes);

        var disposition = resp.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        // ASP.NET ставит attachment по умолчанию для FileStreamResult с FileDownloadName.
        Assert.Equal("attachment", disposition!.DispositionType);
        Assert.Contains("shot.gif", disposition.ToString());
    }

    [Fact(DisplayName = "GET /v2/_internal/attachments/{id}/content без X-Client-Name → 401")]
    public async Task Download_MissingClientName_ReturnsUnauthorized()
    {
        var (_, bugId, _) = await CreateBugAsync();
        var upload = await UploadByBugAsync(bugId, "shot.gif", "image/gif", GifBytes, clientName: "beta-bot");
        var view = await upload.Content.ReadFromJsonAsync<AttachmentView>(JsonOptions);

        var req = new HttpRequestMessage(HttpMethod.Get, $"/v2/_internal/attachments/{view!.Id}/content");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact(DisplayName = "GET /v2/_internal/attachments/{id}/content на несуществующий id → 404")]
    public async Task Download_UnknownAttachment_ReturnsNotFound()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/v2/_internal/attachments/99999999/content");
        req.Headers.Add("X-Client-Name", "beta-bot");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    private Task<(int reportId, int bugId, string testerId)> CreateBugAsync()
        => CreateBugAsync(_client);

    private static async Task<(int reportId, int bugId, string testerId)> CreateBugAsync(HttpClient client)
    {
        var testerId = $"tester_{Guid.NewGuid():N}";
        var payload = new InternalCreateBugRequestDto
        {
            WorkspaceId = $"ws_{Guid.NewGuid():N}",
            CreatorUserId = testerId,
            Receive = "Тестовый receive для attachments",
            Expect = "Тестовый expect для attachments",
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
        return (body!.ReportId, body.BugId, testerId);
    }

    private Task<int> CreateExternalCommentAsync(int bugId, string testerId, string text)
        => CreateExternalCommentAsync(_client, bugId, testerId, text);

    private static async Task<int> CreateExternalCommentAsync(HttpClient client, int bugId, string testerId, string text)
    {
        var payload = new InternalCreateCommentRequestDto
        {
            CreatorType = (int)CreatorType.TgBetaTester,
            CreatorUserId = testerId,
            Text = text,
        };
        var req = new HttpRequestMessage(HttpMethod.Post, $"/v2/_internal/bugs/{bugId}/comments")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        req.Headers.Add("X-Client-Name", "beta-bot");

        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<InternalCreateCommentResponseDto>(JsonOptions);
        return body!.CommentId;
    }

    private Task<HttpResponseMessage> UploadByBugAsync(
        int bugId,
        string fileName,
        string mimeType,
        byte[] content,
        string? clientName)
        => SendUploadAsync(_client, $"/v2/_internal/attachments?bugId={bugId}", fileName, mimeType, content, clientName);

    private Task<HttpResponseMessage> UploadByCommentAsync(
        int commentId,
        string fileName,
        string mimeType,
        byte[] content,
        string? clientName)
        => UploadByCommentAsync(_client, commentId, fileName, mimeType, content, clientName);

    private static Task<HttpResponseMessage> UploadByCommentAsync(
        HttpClient client,
        int commentId,
        string fileName,
        string mimeType,
        byte[] content,
        string? clientName)
        => SendUploadAsync(client, $"/v2/_internal/attachments?commentId={commentId}", fileName, mimeType, content, clientName);

    private static Task<HttpResponseMessage> SendUploadAsync(
        HttpClient client,
        string url,
        string fileName,
        string mimeType,
        byte[] content,
        string? clientName)
    {
        var multipart = BuildMultipart(fileName, mimeType, content);
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = multipart,
        };
        if (clientName is not null)
        {
            req.Headers.Add("X-Client-Name", clientName);
        }

        return client.SendAsync(req);
    }

    private static MultipartFormDataContent BuildMultipart(string fileName, string mimeType, byte[] content)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        multipart.Add(fileContent, "file", fileName);
        return multipart;
    }
}
