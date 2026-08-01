using Bugget.Domain.Search;

namespace Bugget.UnitTests.Search;

public sealed class SortOptionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("created")]
    [InlineData("unknown_desc")]
    public void ParseInvalidValueReturnsDefault(string? value)
    {
        var result = SortOption.Parse(value);

        Assert.Equal("created", result.Field);
        Assert.True(result.IsDescending);
    }

    [Theory]
    [InlineData("created_desc", "created", true)]
    [InlineData("updated_ASC", "updated", false)]
    [InlineData("rank_desc", "rank", true)]
    public void ParseAllowedFieldAndDirection(string value, string field, bool isDescending)
    {
        var result = SortOption.Parse(value);

        Assert.Equal(field, result.Field);
        Assert.Equal(isDescending, result.IsDescending);
    }
}
