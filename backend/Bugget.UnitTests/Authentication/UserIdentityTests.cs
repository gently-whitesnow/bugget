using System.Security.Claims;
using Bugget.Domain.Authentication;
using Bugget.Domain.Common;

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
        Assert.Null(identity.AuthMethod);
        Assert.Equal(CreatorType.User, identity.ActorCreatorType);
    }

    [Fact]
    public void PatAuthMethod_MapsActorCreatorTypeToAgent()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-123"),
                new Claim(AuthClaims.AuthMethod, AuthMethods.Pat),
            ],
            "test",
            ClaimTypes.NameIdentifier,
            ClaimTypes.Role));

        var identity = new UserIdentity(principal);

        Assert.Equal(AuthMethods.Pat, identity.AuthMethod);
        Assert.Equal(CreatorType.Agent, identity.ActorCreatorType);
    }
}
