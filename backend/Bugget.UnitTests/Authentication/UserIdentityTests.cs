using System.Security.Claims;
using Bugget.Domain.Authentication;

namespace Bugget.UnitTests.Authentication;

public sealed class UserIdentityTests
{
    [Fact]
    public void KeepsContextIdentityEmptyWhenClaimsAreAbsent()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-123")],
            "test",
            ClaimTypes.NameIdentifier,
            ClaimTypes.Role));

        var identity = new UserIdentity(principal);

        Assert.Equal("user-123", identity.Id);
        Assert.Null(identity.TeamId);
        Assert.Null(identity.OrganizationId);
        Assert.Null(identity.SignalRConnectionId);
    }
}
