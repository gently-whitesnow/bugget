using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Контракт комментариев (<c>/v2/reports/{aliasId}/bugs/{bugId}/comments</c>) и
/// ссылок репорта (<c>/v2/reports/{aliasId}/links</c>).
/// </summary>
[Collection("PostgresCollection")]
public sealed class CommentsAndLinksContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    [Fact(DisplayName = "POST .../comments: 201 и форма Comment")]
    public async Task CreateComment()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        var response = await scenario.Client.PostAsJsonAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/comments",
            new { text = "комментарий" });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.Created);
        Assert.True(body.GetProperty("id").GetInt32() > 0);
        Assert.Equal("комментарий", body.GetProperty("text").GetString());
        Assert.Equal(bugId, body.GetProperty("bug_id").GetInt32());
        Assert.Equal(scenario.UserId, body.GetProperty("creator_user_id").GetString());
    }

    [Fact(DisplayName = "POST .../comments с пустым text: 400")]
    public async Task CreateEmptyComment()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);

        var response = await scenario.Client.PostAsJsonAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/comments",
            new { text = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await ValidationProblemDetailsContract.AssertSingleErrorAsync(
            response,
            "text",
            "The text field is required.",
            "The field text must be a string with a minimum length of 1 and a maximum length of 2048.");
    }

    [Fact(DisplayName = "PUT .../comments/{commentId}: 200 и форма Comment")]
    public async Task UpdateComment()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var commentId = await scenario.CreateCommentAsync(reportId, bugId);

        var response = await scenario.Client.PutAsJsonAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/comments/{commentId}",
            new { text = "поправленный комментарий" });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(commentId, body.GetProperty("id").GetInt32());
        Assert.Equal("поправленный комментарий", body.GetProperty("text").GetString());
    }

    [Fact(DisplayName = "DELETE .../comments/{commentId}: 200 и пустое тело")]
    public async Task DeleteComment()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var bugId = await scenario.CreateBugAsync(reportId);
        var commentId = await scenario.CreateCommentAsync(reportId, bugId);

        var response = await scenario.Client.DeleteAsync(
            $"/v2/reports/{reportId}/bugs/{bugId}/comments/{commentId}");

        await ContractResponse.EmptyAsync(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName = "POST /v2/reports/{aliasId}/links: 201 и форма ReportLink")]
    public async Task CreateLink()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        var response = await scenario.Client.PostAsJsonAsync(
            $"/v2/reports/{reportId}/links",
            new { link = "https://example.test/issue/1", name = "внешняя задача" });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.Created);
        Assert.True(body.GetProperty("id").GetInt32() > 0);
        Assert.Equal("https://example.test/issue/1", body.GetProperty("link").GetString());
        Assert.Equal("внешняя задача", body.GetProperty("name").GetString());
    }

    [Fact(DisplayName = "PUT /v2/reports/{aliasId}/links/{linkId}: 200 и форма ReportLink")]
    public async Task UpdateLink()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var linkId = await scenario.CreateLinkAsync(reportId);

        var response = await scenario.Client.PutAsJsonAsync(
            $"/v2/reports/{reportId}/links/{linkId}",
            new { link = "https://example.test/issue/2", name = "другая задача" });

        var body = await ContractResponse.JsonAsync(response, HttpStatusCode.OK);
        Assert.Equal(linkId, body.GetProperty("id").GetInt32());
        Assert.Equal("https://example.test/issue/2", body.GetProperty("link").GetString());
        Assert.Equal("другая задача", body.GetProperty("name").GetString());
    }

    [Fact(DisplayName = "DELETE /v2/reports/{aliasId}/links/{linkId}: 200 и пустое тело")]
    public async Task DeleteLink()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var linkId = await scenario.CreateLinkAsync(reportId);

        var response = await scenario.Client.DeleteAsync($"/v2/reports/{reportId}/links/{linkId}");

        await ContractResponse.EmptyAsync(response, HttpStatusCode.OK);
    }
}
