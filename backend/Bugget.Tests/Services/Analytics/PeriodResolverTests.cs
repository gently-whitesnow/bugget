using Bugget.BO.Services.Analytics;
using FluentAssertions;
using Xunit;

namespace Bugget.Tests.Services.Analytics;

/// <summary>
/// PeriodResolver: маппинг строкового wire-ключа периода в окно [from; to) + ярлык.
/// После R6 принимает <see cref="string"/>, а не enum — валидация значений
/// делается здесь же; на невалидный input — <see cref="ArgumentException"/>.
/// </summary>
public sealed class PeriodResolverTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("7d", 7, "last_7_days")]
    [InlineData("30d", 30, "last_30_days")]
    [InlineData("60d", 60, "last_60_days")]
    [InlineData("180d", 180, "last_180_days")]
    [InlineData("360d", 360, "last_360_days")]
    public void Resolve_DaysWindow_ReturnsExpectedRange(string period, int days, string expectedLabel)
    {
        var window = PeriodResolver.Resolve(period, Now);

        window.To.Should().Be(Now);
        window.From.Should().Be(Now - TimeSpan.FromDays(days));
        window.Label.Should().Be(expectedLabel);
    }

    [Fact]
    public void Resolve_All_StartsFromEpoch()
    {
        var window = PeriodResolver.Resolve("all", Now);

        window.To.Should().Be(Now);
        window.From.Should().Be(new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero));
        window.Label.Should().Be("all_time");
    }

    [Fact]
    public void Resolve_UnknownPeriodValue_ThrowsArgumentException()
    {
        var act = () => PeriodResolver.Resolve("42d", Now);

        act.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("period");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_NullOrWhitespace_ThrowsArgumentException(string? period)
    {
        var act = () => PeriodResolver.Resolve(period, Now);

        act.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("period");
    }

    [Fact]
    public void Resolve_CaseSensitive_RejectsUppercase()
    {
        // Wire-формат фиксирован lowercase'ом — не принимаем варианты.
        var act = () => PeriodResolver.Resolve("7D", Now);

        act.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("period");
    }

    /// <summary>
    /// Список допустимых значений публичен и попадает в тело ответа 400, поэтому он
    /// обязан совпадать с тем, что резолвер реально принимает: разъехавшись, он начнёт
    /// врать клиенту.
    /// </summary>
    [Fact]
    public void AllowedValues_matches_what_the_resolver_accepts()
    {
        foreach (var value in PeriodResolver.AllowedValues)
        {
            var act = () => PeriodResolver.Resolve(value, Now);

            act.Should().NotThrow($"значение {value} объявлено допустимым");
        }
    }
}
