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

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.comments.post", response);
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
        await ContractSnapshot.MatchAsync("v2.comments.post.invalid", response);
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.comments.put", response);
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.comments.delete", response);
    }

    [Fact(DisplayName = "POST /v2/reports/{aliasId}/links: 201 и форма ReportLink")]
    public async Task CreateLink()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();

        var response = await scenario.Client.PostAsJsonAsync(
            $"/v2/reports/{reportId}/links",
            new { link = "https://example.test/issue/1", name = "внешняя задача" });

        await ContractSnapshot.MatchAsync("v2.report-links.post", response);
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.report-links.put", response);
    }

    [Fact(DisplayName = "DELETE /v2/reports/{aliasId}/links/{linkId}: 200 и пустое тело")]
    public async Task DeleteLink()
    {
        var scenario = ContractScenario.Create(fixture);
        var reportId = await scenario.CreateReportAsync();
        var linkId = await scenario.CreateLinkAsync(reportId);

        var response = await scenario.Client.DeleteAsync($"/v2/reports/{reportId}/links/{linkId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractSnapshot.MatchAsync("v2.report-links.delete", response);
    }
}
