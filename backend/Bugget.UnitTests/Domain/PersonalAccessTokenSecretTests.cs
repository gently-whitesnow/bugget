using Bugget.Domain.Users;
using FluentAssertions;

namespace Bugget.UnitTests.Domain;

public sealed class PersonalAccessTokenSecretTests
{
    [Fact]
    public void Generate_ProducesPrefixedValueWithMatchingHash()
    {
        var generated = PersonalAccessTokenSecret.Generate();

        generated.Value.Should().StartWith(PersonalAccessTokenSecret.Prefix);
        generated.DisplayPrefix.Should().StartWith(PersonalAccessTokenSecret.Prefix);
        generated.Value.Should().StartWith(generated.DisplayPrefix);
        generated.Hash.Should().Equal(PersonalAccessTokenSecret.ComputeHash(generated.Value));
    }

    [Fact]
    public void Generate_DoesNotRepeatSecrets()
    {
        var values = Enumerable.Range(0, 100)
            .Select(_ => PersonalAccessTokenSecret.Generate().Value)
            .ToArray();

        values.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void DisplayPrefix_ShowsOnlyOpeningOfSecret()
    {
        var generated = PersonalAccessTokenSecret.Generate();

        // Открытая часть — префикс значения и заметно короче него: по ней токен узнают
        // в списке, но восстановить из неё секрет нельзя.
        generated.Value.Should().StartWith(generated.DisplayPrefix);
        generated.DisplayPrefix.Length.Should().BeLessThan(generated.Value.Length / 2);
    }

    [Fact]
    public void ComputeHash_IsStableAndDiffersPerValue()
    {
        var first = PersonalAccessTokenSecret.Generate().Value;
        var second = PersonalAccessTokenSecret.Generate().Value;

        PersonalAccessTokenSecret.ComputeHash(first)
            .Should().Equal(PersonalAccessTokenSecret.ComputeHash(first));
        PersonalAccessTokenSecret.ComputeHash(first)
            .Should().NotEqual(PersonalAccessTokenSecret.ComputeHash(second));
    }

    [Fact]
    public void ComputeHash_ProducesSha256Length()
    {
        PersonalAccessTokenSecret.ComputeHash("bgt_pat_whatever").Should().HaveCount(32);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("eyJhbGciOiJSUzUxMiJ9.payload.signature", false)]
    [InlineData("bgt_pat_", false)]
    [InlineData("bgt_pat_short", false)]
    [InlineData("BGT_PAT_abcdefghijklmnop", false)]
    [InlineData("bgt_pat_abcdefghijklmnop", true)]
    public void HasValidFormat_SeparatesPatFromOtherBearerValues(string? value, bool expected)
    {
        PersonalAccessTokenSecret.HasValidFormat(value).Should().Be(expected);
    }
}
