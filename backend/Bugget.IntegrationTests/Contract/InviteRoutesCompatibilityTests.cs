using Xunit;

namespace Bugget.IntegrationTests.Contract;

/// <summary>
/// Регрессионное доказательство для публичных invite-маршрутов, которые до их удаления
/// были помечены инвентарём как вызываемые фронтом. Канон требует сохранять URL 1:1 либо
/// фиксировать breaking change действующей ADR.
/// </summary>
[Collection("PostgresCollection")]
public sealed class InviteRoutesCompatibilityTests(AppContractFixture fixture) : IClassFixture<AppContractFixture>
{
    private static readonly string[] ExpectedRoutes =
    [
        "DELETE /v1/workspaces/{workspaceId}/teams/{teamId}/invites/{id}",
        "GET /v1/workspaces/{workspaceId}/teams/{teamId}/invites",
        "POST /v1/invites/accept",
        "POST /v1/workspaces/{workspaceId}/teams/{teamId}/invites",
        "PUT /v1/workspaces/{workspaceId}/teams/{teamId}/invites/{id}",
    ];

    [Fact(DisplayName = "Публичные invite URL сохранены после AI-ready миграции")]
    public void PublicInviteRoutesAreStillPublished()
    {
        fixture.CreateAnonymousClient();
        var actual = PublicSurface.Routes(fixture.Services);
        var missing = ExpectedRoutes.Where(route => !actual.Contains(route, StringComparer.Ordinal)).ToArray();

        Assert.Empty(missing);
    }
}
