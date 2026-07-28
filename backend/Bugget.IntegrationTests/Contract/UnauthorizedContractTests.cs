using System.Net;
using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Поведение на запросе без identity. В бою заголовки <c>Auth-Request-*</c> ставит
/// nginx после успешного <c>auth_request</c>; запрос без них — это запрос, который
/// авторизацию не прошёл, и отвечать на него данными нельзя.
/// </summary>
[Collection("PostgresCollection")]
public sealed class UnauthorizedContractTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    public static TheoryData<string, string> ReportsModulePaths() => new()
    {
        { "GET", "/v2/reports" },
        { "POST", "/v2/reports" },
        { "GET", "/v2/reports/1" },
        { "PATCH", "/v2/reports/1" },
        { "GET", "/v2/reports/legacy/1" },
        { "GET", "/v2/reports/1/analytics" },
        { "POST", "/v2/reports/counts:batch" },
        { "POST", "/v2/reports/1/bugs" },
        { "PATCH", "/v2/reports/1/bugs/1" },
        { "POST", "/v2/reports/1/bugs/1/steps" },
        { "PUT", "/v2/reports/1/bugs/1/steps/order" },
        { "POST", "/v2/reports/1/bugs/1/comments" },
        { "PUT", "/v2/reports/1/bugs/1/comments/1" },
        { "DELETE", "/v2/reports/1/bugs/1/comments/1" },
        { "GET", "/v2/reports/1/bugs/1/attachments/1/content" },
        { "GET", "/v2/reports/1/bugs/1/attachments/1/content/preview" },
        { "GET", "/v2/reports/1/bugs/1/comments/1/attachments/1/content" },
        { "GET", "/v2/reports/1/bugs/1/steps/1/attachments/1/content" },
        { "POST", "/v2/reports/1/links" },
        { "PUT", "/v2/reports/1/links/1" },
        { "DELETE", "/v2/reports/1/links/1" },
        { "GET", "/v2/analytics/summary?period=30d" },
        { "GET", "/v2/analytics/responsible/user-1?period=30d" },
        { "GET", "/v1/reports/search" },
        { "GET", "/v1/external/search" },
        { "POST", "/v1/external/search/apply" },
        { "GET", "/v1/external/kaiten/boards" },
        { "POST", "/v1/external/kaiten/boards/batch-get" },
        { "GET", "/v1/settings-sections" },
        { "PUT", "/v1/workspace-settings-sections/kaiten/settings/domain" },
        { "PUT", "/v1/team-settings-sections/kaiten/settings/use_report_linking" },
        { "PUT", "/v1/user-settings-sections/kaiten/settings/any" },
    };

    public static TheoryData<string, string> UsersModulePaths() => new()
    {
        { "GET", "/v1/workspaces" },
        { "POST", "/v1/workspaces" },
        { "PUT", "/v1/workspaces/1" },
        { "DELETE", "/v1/workspaces/1" },
        { "POST", "/v1/workspaces/1/members/join" },
        { "POST", "/v1/workspaces/1/teams" },
        { "PUT", "/v1/workspaces/1/teams/1" },
        { "DELETE", "/v1/workspaces/1/teams/1" },
        { "POST", "/v1/workspaces/1/teams/batch/list" },
        { "GET", "/v1/workspaces/1/teams/autocomplete" },
        { "GET", "/v1/workspaces/1/teams/1/members" },
        { "POST", "/v1/workspaces/1/teams/1/members/join" },
        { "DELETE", "/v1/workspaces/1/teams/1/members" },
        { "GET", "/v1/workspaces/1/teams/1/users" },
        { "PUT", "/v1/workspaces/1/teams/1/users" },
        { "POST", "/v1/workspaces/1/teams/1/users/batch/list" },
        { "GET", "/v1/workspaces/1/teams/1/users/autocomplete" },
        { "GET", "/v1/workspaces/1/teams/1/users/external-links" },
        { "POST", "/v1/workspaces/1/teams/1/users/merge" },
    };

    [Fact(DisplayName = "Загрузка вложения без identity: 401")]
    public async Task AttachmentUploadRequiresIdentity()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsync(
            "/v2/reports/1/bugs/1/attachments?attachType=0",
            ContractScenario.FileContent("shot.png"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory(DisplayName = "Без identity-заголовков модуль reports отвечает 401")]
    [MemberData(nameof(ReportsModulePaths))]
    public async Task ReportsModuleRequiresIdentity(string method, string path)
    {
        var response = await SendAsync(method, path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Модуль users на неавторизованный запрос отвечает 401 либо 404: часть его ручек
    /// закрыта фильтрами <c>WorkspaceRequired</c>/<c>TeamRequired</c>, которые прячут
    /// существование ресурса. Оба варианта означают одно: данных без identity нет.
    /// </summary>
    [Theory(DisplayName = "Без identity-заголовков модуль users не отдаёт данные")]
    [MemberData(nameof(UsersModulePaths))]
    public async Task UsersModuleRequiresIdentity(string method, string path)
    {
        var response = await SendAsync(method, path);

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound
                or HttpStatusCode.Forbidden,
            $"{method} {path} без identity вернул {(int)response.StatusCode}");
    }

    private async Task<HttpResponseMessage> SendAsync(string method, string path)
    {
        var client = fixture.CreateAnonymousClient();
        var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method is "POST" or "PUT" or "PATCH"
                ? new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                : null,
        };

        return await client.SendAsync(request);
    }
}
