using Bugget.Domain.Users;
using FluentAssertions;

namespace Bugget.UnitTests.Domain;

public sealed class PersonalAccessTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    private static PersonalAccessToken Token(DateTimeOffset? expiresAt = null, DateTimeOffset? revokedAt = null) =>
        new()
        {
            Id = 1,
            UserId = 42,
            WorkspaceId = 1,
            TeamId = 2,
            Label = "mcp",
            TokenPrefix = "bgt_pat_abcdef",
            CreatedAt = Now.AddDays(-1),
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt
        };

    [Fact]
    public void WithoutExpiry_NeverExpires()
    {
        var token = Token();

        token.IsExpired(Now.AddYears(10)).Should().BeFalse();
        token.IsUsable(Now.AddYears(10)).Should().BeTrue();
    }

    [Fact]
    public void ExpiryInFuture_IsUsable()
    {
        Token(expiresAt: Now.AddDays(1)).IsUsable(Now).Should().BeTrue();
    }

    [Fact]
    public void ExpiryExactlyNow_IsAlreadyExpired()
    {
        var token = Token(expiresAt: Now);

        token.IsExpired(Now).Should().BeTrue();
        token.IsUsable(Now).Should().BeFalse();
    }

    [Fact]
    public void Revoked_IsNotUsableEvenWhileUnexpired()
    {
        var token = Token(expiresAt: Now.AddDays(30), revokedAt: Now.AddHours(-1));

        token.IsRevoked.Should().BeTrue();
        token.IsExpired(Now).Should().BeFalse();
        token.IsUsable(Now).Should().BeFalse();
    }

    [Fact]
    public void ResolveExpiresAt_WithoutRequest_AppliesDefaultLifetime()
    {
        PersonalAccessToken.ResolveExpiresAt(Now, requested: null)
            .Should().Be(Now.Add(PersonalAccessToken.DefaultLifetime));
    }

    [Fact]
    public void ResolveExpiresAt_KeepsExplicitRequest()
    {
        var requested = Now.AddDays(7);

        PersonalAccessToken.ResolveExpiresAt(Now, requested).Should().Be(requested);
    }
}
