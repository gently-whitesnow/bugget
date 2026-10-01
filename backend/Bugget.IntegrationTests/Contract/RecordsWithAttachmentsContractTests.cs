using System.Net;
using System.Text.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Комментарий и шаг вместе с вложениями создаются одной транзакцией (ADR-0015):
/// отказ по любому файлу не оставляет после себя записи-владельца.
/// </summary>
[Collection("PostgresCollection")]
public sealed class RecordsWithAttachmentsContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    private const string FileWithoutExtension = "noext";

    [Theory(DisplayName = "POST .../with-attachments: 201, запись и все вложения в ответе")]
    [InlineData("comments", "comment")]
    [InlineData("steps", "bug_step")]
    public async Task CreatesRecordWithAllAttachments(string records, string attachType)
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);

        var response = await scenario.Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/{records}/with-attachments",
            Form("with files", "first.png", "second.png"));

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.Created);
        Assert.Equal("with files", body.GetProperty("text").GetString());

        var attachments = body.GetProperty("attachments").EnumerateArray().ToArray();
        Assert.Equal(["first.png", "second.png"], attachments.Select(a => a.GetProperty("file_name").GetString()));
        Assert.All(attachments, attachment =>
        {
            Assert.Equal(attachType, attachment.GetProperty("attach_type").GetString());
            Assert.Equal(body.GetProperty("id").GetInt32(), attachment.GetProperty("entity_id").GetInt32());
        });
    }

    [Theory(DisplayName = "POST .../with-attachments с негодным файлом: 400 и запись не создана")]
    [InlineData("comments")]
    [InlineData("steps")]
    public async Task RejectedFileLeavesNoRecord(string records)
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);

        var response = await scenario.Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/{records}/with-attachments",
            Form("must not appear", "good.png", FileWithoutExtension));

        await ContractResponse.ProblemAsync(response, "attachment_file_extension_not_found", HttpStatusCode.BadRequest);

        var report = await ContractScenario.ReadJsonAsync(await scenario.Client.GetAsync($"/v2/reports/{reportId}"));
        var bug = report.GetProperty("bugs").EnumerateArray().Single(b => b.GetProperty("id").GetInt32() == bugId);
        Assert.Equal(0, CountOrZero(bug.GetProperty(records)));
    }

    [Theory(DisplayName = "POST .../with-attachments без файлов: 400 attachment_file_not_selected_or_empty")]
    [InlineData("comments")]
    [InlineData("steps")]
    public async Task RequiresAtLeastOneFile(string records)
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);

        var response = await scenario.Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/{records}/with-attachments",
            Form("text only"));

        await ContractResponse.ProblemAsync(response, "attachment_file_not_selected_or_empty", HttpStatusCode.BadRequest);
    }

    [Theory(DisplayName = "POST .../with-attachments без текста: 400 validation_error")]
    [InlineData("comments")]
    [InlineData("steps")]
    public async Task RequiresText(string records)
    {
        var scenario = ContractScenario.Create(fixture);
        var (reportId, bugId) = await CreateBugAsync(scenario);

        var response = await scenario.Client.PostAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/{records}/with-attachments",
            Form(null, "shot.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static int CountOrZero(JsonElement array) =>
        array.ValueKind == JsonValueKind.Array ? array.GetArrayLength() : 0;

    private static MultipartFormDataContent Form(string? text, params string[] fileNames)
    {
        var form = new MultipartFormDataContent();
        if (text is not null)
        {
            form.Add(new StringContent(text), "text");
        }

        foreach (var fileName in fileNames)
        {
            var file = ContractScenario.FileContent(fileName).Single();
            file.Headers.ContentDisposition = null;
            form.Add(file, "files", fileName);
        }

        return form;
    }

    private static async Task<(string ReportId, int BugId)> CreateBugAsync(ContractScenario scenario)
    {
        var reportId = await scenario.CreateReportAsync();
        return (reportId, await scenario.CreateBugAsync(reportId));
    }
}
