using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контракт вложений. Семейств три — у бага, у комментария и у шага, — и у каждого
/// свой набор путей. Правила у них одни (владелец в <c>entity_id</c>, тип в
/// <c>attach_type</c>, картинка на выходе в webp), но проверяются на всех трёх:
/// расходятся такие вещи молча.
/// </summary>
[Collection("PostgresCollection")]
public sealed class AttachmentsContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    [Fact(DisplayName = "POST .../bugs/{bugId}/attachments: 201 и форма Attachment")]
    public async Task UploadBugAttachment()
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);

        var response = await scenario.Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/attachments?attachType=0",
            ContractScenario.FileContent("shot.png"));

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.Created);
        Assert.Equal("shot.png", body.GetProperty("file_name").GetString());
        Assert.Equal(0, body.GetProperty("attach_type").GetInt32());
        Assert.Equal(bugId, body.GetProperty("entity_id").GetInt32());
    }

    [Fact(DisplayName = "POST .../bugs/{bugId}/attachments без attachType: 400")]
    public async Task UploadBugAttachmentWithoutType()
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);

        var response = await scenario.Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/attachments",
            ContractScenario.FileContent("shot.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await ValidationProblemDetailsContract.AssertSingleErrorAsync(
            response,
            "attachType",
            "The attachType field is required.");
    }

    /// <summary>
    /// `attachType` у баговой ручки приходит из query, и на проводе это просто `integer`.
    /// Вложения всех контекстов лежат в одной таблице и разводятся по владельцам только
    /// значением `attach_type`, поэтому чужое значение (2 — комментарий, 3 — шаг) или
    /// значение вне 0..3 не должно доезжать до хранилища: иначе вложение бага всплыло бы
    /// у комментария или шага с тем же идентификатором. Тест фиксирует, что это уже
    /// закрыто доменной ошибкой, а не только описанием в контракте.
    /// </summary>
    [Theory(DisplayName = "POST .../bugs/{bugId}/attachments с чужим или неизвестным attachType: 400")]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(99)]
    [InlineData(-1)]
    public async Task UploadBugAttachmentWithForeignType(int attachType)
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);

        var response = await scenario.Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/attachments?attachType={attachType}",
            ContractScenario.FileContent("shot.png"));

        await ContractResponse.ProblemAsync(
            response,
            "attachment_type_not_allowed",
            HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "GET .../attachments/{id}/content: содержимое вложения")]
    public async Task DownloadBugAttachment()
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);
        var attachmentId = await UploadAsync(scenario, $"/v2/reports/{reportId}/bugs/{bugId}/attachments?attachType=0");

        var response = await scenario.Client.GetAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/attachments/{attachmentId}/content");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Картинки хранятся оптимизированными: PNG на входе, webp на выходе.
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact(DisplayName = "GET .../attachments/{id}/content/preview: превью в webp")]
    public async Task DownloadBugAttachmentPreview()
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);
        var attachmentId = await UploadAsync(scenario, $"/v2/reports/{reportId}/bugs/{bugId}/attachments?attachType=0");

        var response = await scenario.Client.GetAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/attachments/{attachmentId}/content/preview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName = "PATCH .../attachments/{id}: 200 и форма Attachment")]
    public async Task RenameBugAttachment()
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);
        var attachmentId = await UploadAsync(scenario, $"/v2/reports/{reportId}/bugs/{bugId}/attachments?attachType=0");

        var response = await scenario.Client.PatchAsJsonAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/attachments/{attachmentId}",
            new { file_name = "renamed.png" });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(attachmentId, body.GetProperty("id").GetInt32());
        Assert.Equal("renamed.png", body.GetProperty("file_name").GetString());
    }

    [Fact(DisplayName = "DELETE .../attachments/{id}: 200 и пустое тело")]
    public async Task DeleteBugAttachment()
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);
        var attachmentId = await UploadAsync(scenario, $"/v2/reports/{reportId}/bugs/{bugId}/attachments?attachType=0");

        var response = await scenario.Client.DeleteAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/attachments/{attachmentId}");

        await ContractResponse.EmptyAsync(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Вложения комментария: POST, PATCH, content, preview, DELETE")]
    public async Task CommentAttachmentLifecycle()
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);
        var commentId = await scenario.CreateCommentAsync(reportId, bugId);
        var basePath = $"/v2/reports/{reportId}/bugs/{bugId}/comments/{commentId}/attachments";

        var created = await scenario.Client.PostAsync(basePath, ContractScenario.FileContent("comment.png"));
        var createdBody = await ContractResponse.JsonAsync(created, HttpStatusCode.Created);

        // Тип вложения комментария ставит сервер, а владелец — сам комментарий.
        Assert.Equal(2, createdBody.GetProperty("attach_type").GetInt32());
        Assert.Equal(commentId, createdBody.GetProperty("entity_id").GetInt32());

        var attachmentId = createdBody.GetProperty("id").GetInt32();

        var renamed = await scenario.Client.PatchAsJsonAsync(
            $"{basePath}/{attachmentId}",
            new { file_name = "renamed.png" });
        var renamedBody = await ContractResponse.JsonAsync(renamed, HttpStatusCode.OK);
        Assert.Equal("renamed.png", renamedBody.GetProperty("file_name").GetString());

        var content = await scenario.Client.GetAsync($"{basePath}/{attachmentId}/content");
        Assert.Equal("image/webp", content.Content.Headers.ContentType?.MediaType);

        var preview = await scenario.Client.GetAsync($"{basePath}/{attachmentId}/content/preview");
        Assert.Equal("image/webp", preview.Content.Headers.ContentType?.MediaType);

        var deleted = await scenario.Client.DeleteAsync($"{basePath}/{attachmentId}");
        await ContractResponse.EmptyAsync(deleted, HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Вложения шага: POST, PATCH, content, preview, DELETE")]
    public async Task BugStepAttachmentLifecycle()
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);
        var stepId = await scenario.CreateStepAsync(reportId, bugId);
        var basePath = $"/v2/reports/{reportId}/bugs/{bugId}/steps/{stepId}/attachments";

        var created = await scenario.Client.PostAsync(basePath, ContractScenario.FileContent("step.png"));
        var createdBody = await ContractResponse.JsonAsync(created, HttpStatusCode.Created);

        // Тип вложения шага ставит сервер, а владелец — сам шаг.
        Assert.Equal(3, createdBody.GetProperty("attach_type").GetInt32());
        Assert.Equal(stepId, createdBody.GetProperty("entity_id").GetInt32());

        var attachmentId = createdBody.GetProperty("id").GetInt32();

        var renamed = await scenario.Client.PatchAsJsonAsync(
            $"{basePath}/{attachmentId}",
            new { file_name = "renamed.png" });
        var renamedBody = await ContractResponse.JsonAsync(renamed, HttpStatusCode.OK);
        Assert.Equal("renamed.png", renamedBody.GetProperty("file_name").GetString());

        var content = await scenario.Client.GetAsync($"{basePath}/{attachmentId}/content");
        Assert.Equal("image/webp", content.Content.Headers.ContentType?.MediaType);

        var preview = await scenario.Client.GetAsync($"{basePath}/{attachmentId}/content/preview");
        Assert.Equal("image/webp", preview.Content.Headers.ContentType?.MediaType);

        var deleted = await scenario.Client.DeleteAsync($"{basePath}/{attachmentId}");
        await ContractResponse.EmptyAsync(deleted, HttpStatusCode.OK);
    }

    private static async Task<(string ReportId, int BugId)> CreateBugAsync(ContractScenario scenario)
    {
        var reportId = await scenario.CreateReportAsync();
        return (reportId, await scenario.CreateBugAsync(reportId));
    }

    private static async Task<int> UploadAsync(ContractScenario scenario, string path)
    {
        var response = await scenario.Client.PostAsync(path, ContractScenario.FileContent("shot.png"));
        return (await ContractScenario.ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }
}
