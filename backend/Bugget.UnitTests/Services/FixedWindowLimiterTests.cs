using Bugget.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Bugget.UnitTests.Services;

public sealed class FixedWindowLimiterTests
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-08-07T12:00:00Z"));

    [Fact]
    public void TryAcquire_AllowsUpToLimitPerWindow()
    {
        var limiter = new FixedWindowLimiter(_time, limit: 3, window: TimeSpan.FromMinutes(1));

        limiter.TryAcquire("agent").Should().BeTrue();
        limiter.TryAcquire("agent").Should().BeTrue();
        limiter.TryAcquire("agent").Should().BeTrue();
        limiter.TryAcquire("agent").Should().BeFalse();

        // Другой ключ живёт в своём окне.
        limiter.TryAcquire("other").Should().BeTrue();
    }

    [Fact]
    public void WindowExpiresWithTime()
    {
        var limiter = new FixedWindowLimiter(_time, limit: 1, window: TimeSpan.FromMinutes(1));

        limiter.TryAcquire("agent").Should().BeTrue();
        limiter.TryAcquire("agent").Should().BeFalse();

        _time.Advance(TimeSpan.FromMinutes(2));

        limiter.TryAcquire("agent").Should().BeTrue();
    }

    [Fact]
    public void IsLimited_ChecksWithoutRecording()
    {
        var limiter = new FixedWindowLimiter(_time, limit: 2, window: TimeSpan.FromMinutes(5));

        // Сколько ни спрашивай — счёт не растёт.
        for (var i = 0; i < 10; i++)
        {
            limiter.IsLimited("prefix").Should().BeFalse();
        }

        limiter.Record("prefix");
        limiter.IsLimited("prefix").Should().BeFalse();

        limiter.Record("prefix");
        limiter.IsLimited("prefix").Should().BeTrue();

        _time.Advance(TimeSpan.FromMinutes(6));
        limiter.IsLimited("prefix").Should().BeFalse();
    }
}
