using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контракт вложений. Семейств три — у бага, у комментария и у шага, — и у каждого
/// свой набор путей. Форма ответа у них одна (<c>AttachmentView</c>), но проверяется
/// на всех трёх: расходятся такие вещи молча.
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

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.bug-attachments.post", response);
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
        await ContractSnapshot.MatchAsync("v2.bug-attachments.post.invalid", response);
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.bug-attachments.patch", response);
    }

    [Fact(DisplayName = "DELETE .../attachments/{id}: 200 и пустое тело")]
    public async Task DeleteBugAttachment()
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);
        var attachmentId = await UploadAsync(scenario, $"/v2/reports/{reportId}/bugs/{bugId}/attachments?attachType=0");

        var response = await scenario.Client.DeleteAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.bug-attachments.delete", response);
    }

    [Fact(DisplayName = "Вложения комментария: POST, PATCH, content, preview, DELETE")]
    public async Task CommentAttachmentLifecycle()
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);
        var commentId = await scenario.CreateCommentAsync(reportId, bugId);
        var basePath = $"/v2/reports/{reportId}/bugs/{bugId}/comments/{commentId}/attachments";

        var created = await scenario.Client.PostAsync(basePath, ContractScenario.FileContent("comment.png"));
        await ContractSnapshot.MatchAsync("v2.comment-attachments.post", created);

        var attachmentId = (await ContractScenario.ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var renamed = await scenario.Client.PatchAsJsonAsync(
            $"{basePath}/{attachmentId}",
            new { file_name = "renamed.png" });
        await ContractSnapshot.MatchAsync("v2.comment-attachments.patch", renamed);

        var content = await scenario.Client.GetAsync($"{basePath}/{attachmentId}/content");
        Assert.Equal("image/webp", content.Content.Headers.ContentType?.MediaType);

        var preview = await scenario.Client.GetAsync($"{basePath}/{attachmentId}/content/preview");
        Assert.Equal("image/webp", preview.Content.Headers.ContentType?.MediaType);

        var deleted = await scenario.Client.DeleteAsync($"{basePath}/{attachmentId}");
        await ContractSnapshot.MatchAsync("v2.comment-attachments.delete", deleted);
    }

    [Fact(DisplayName = "Вложения шага: POST, PATCH, content, preview, DELETE")]
    public async Task BugStepAttachmentLifecycle()
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);
        var stepId = await scenario.CreateStepAsync(reportId, bugId);
        var basePath = $"/v2/reports/{reportId}/bugs/{bugId}/steps/{stepId}/attachments";

        var created = await scenario.Client.PostAsync(basePath, ContractScenario.FileContent("step.png"));
        await ContractSnapshot.MatchAsync("v2.bug-step-attachments.post", created);

        var attachmentId = (await ContractScenario.ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var renamed = await scenario.Client.PatchAsJsonAsync(
            $"{basePath}/{attachmentId}",
            new { file_name = "renamed.png" });
        await ContractSnapshot.MatchAsync("v2.bug-step-attachments.patch", renamed);

        var content = await scenario.Client.GetAsync($"{basePath}/{attachmentId}/content");
        Assert.Equal("image/webp", content.Content.Headers.ContentType?.MediaType);

        var preview = await scenario.Client.GetAsync($"{basePath}/{attachmentId}/content/preview");
        Assert.Equal("image/webp", preview.Content.Headers.ContentType?.MediaType);

        var deleted = await scenario.Client.DeleteAsync($"{basePath}/{attachmentId}");
        await ContractSnapshot.MatchAsync("v2.bug-step-attachments.delete", deleted);
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
