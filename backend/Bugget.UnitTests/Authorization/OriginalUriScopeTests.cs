using Bugget.Api.Authorization.Authentication;

namespace Bugget.UnitTests.Authorization;

public sealed class OriginalUriScopeTests
{
    [Theory]
    [InlineData("/v1/workspaces/12/teams/34/reports", 12, 34)]
    [InlineData("http://localhost/v1/workspaces/1/teams/2", 1, 2)]
    public void TryParse_ReadsWorkspaceAndTeam(string uri, int workspaceId, int teamId)
    {
        Assert.True(OriginalUriScope.TryParse(uri, out var parsedWorkspaceId, out var parsedTeamId));
        Assert.Equal(workspaceId, parsedWorkspaceId);
        Assert.Equal(teamId, parsedTeamId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/v1/workspaces/12/reports")]
    [InlineData("/v1/teams/34")]
    public void TryParse_RequiresBothIds(string? uri)
    {
        Assert.False(OriginalUriScope.TryParse(uri, out _, out _));
    }

    [Fact]
    public void ParseOptional_AllowsPartialUri()
    {
        var (workspaceId, teamId) = OriginalUriScope.ParseOptional("/v1/workspaces/9/reports");

        Assert.Equal(9, workspaceId);
        Assert.Null(teamId);
    }
}
