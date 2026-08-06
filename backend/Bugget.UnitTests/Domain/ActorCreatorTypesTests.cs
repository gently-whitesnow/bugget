using Bugget.Domain.Authentication;
using Bugget.Domain.Common;
using FluentAssertions;

namespace Bugget.UnitTests.Domain;

public sealed class ActorCreatorTypesTests
{
    [Theory]
    [InlineData(null, CreatorType.User)]
    [InlineData("", CreatorType.User)]
    [InlineData(AuthMethods.Jwt, CreatorType.User)]
    [InlineData(AuthMethods.Pat, CreatorType.Agent)]
    [InlineData("PAT", CreatorType.User)]
    public void FromAuthMethod_MapsExpected(string? authMethod, CreatorType expected)
    {
        ActorCreatorTypes.FromAuthMethod(authMethod).Should().Be(expected);
    }
}
