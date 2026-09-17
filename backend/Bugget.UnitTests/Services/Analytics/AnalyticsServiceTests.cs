using Bugget.Application.Services.Analytics;
using Bugget.Domain.Analytics;
using FluentAssertions;
using Xunit;

namespace Bugget.UnitTests.Services.Analytics;

/// <summary>AnalyticsService.ComputeSummary — pure-функция, без моков.</summary>
public sealed class AnalyticsServiceTests
{
    private static readonly PeriodWindow Window = new()
    {
        From = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        To = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
        Label = "last_30_days",
    };

    [Fact]
    public void ComputeSummary_EmptyRaw_ReportsClosedZero_NullableNullsCorrect()
    {
        var raw = new AnalyticsRawData
        {
            ClosedReports = Array.Empty<ClosedReportRow>(),
            PhaseAggregates = Array.Empty<PhaseAggregateRow>(),
            PhaseTrendsWeekly = Array.Empty<PhaseTrendWeeklyBo>(),
        };

        var summary = AnalyticsService.ComputeSummary(Window, raw);

        summary.ReportsClosed.Should().Be(0);
        summary.AvgFullCycleDays.Should().BeNull();
        summary.ReworkRate.Should().Be(0.0);
        summary.AvgRegressionCyclesWhenPresent.Should().BeNull();
        summary.AvgTestRetestDays.Should().BeNull();
        summary.AvgFixDays.Should().BeNull();
        summary.AvgTestInitialDays.Should().Be(0.0);
        summary.TestPct.Should().Be(0.0);
        summary.FixPct.Should().Be(0.0);
        summary.TopRegressionReports.Should().BeEmpty();
        summary.PhaseTrendsWeekly.Should().BeEmpty();
    }

    [Fact]
    public void ComputeSummary_ReworkRate_ComputedAsRegressedOverTotal()
    {
        // 4 closed reports, 1 c регрессией → rework_rate = 0.25
        var raw = BuildRaw(
            closedReports: new[]
            {
                MakeReport(reportId: 1, testIntervals: 1, fixIntervals: 0),
                MakeReport(reportId: 2, testIntervals: 1, fixIntervals: 0),
                MakeReport(reportId: 3, testIntervals: 1, fixIntervals: 0),
                MakeReport(reportId: 4, testIntervals: 2, fixIntervals: 1),
            },
            phaseAggregates: Array.Empty<PhaseAggregateRow>());

        var summary = AnalyticsService.ComputeSummary(Window, raw);

        summary.ReworkRate.Should().BeApproximately(0.25, 1e-9);
        summary.ReportsClosed.Should().Be(4);
    }

    [Fact]
    public void ComputeSummary_AvgRegressionCyclesWhenPresent_NullOnNoRegression()
    {
        var raw = BuildRaw(
            closedReports: new[]
            {
                MakeReport(reportId: 1, testIntervals: 1, fixIntervals: 0),
                MakeReport(reportId: 2, testIntervals: 1, fixIntervals: 0),
            },
            phaseAggregates: Array.Empty<PhaseAggregateRow>());

        var summary = AnalyticsService.ComputeSummary(Window, raw);

        summary.AvgRegressionCyclesWhenPresent.Should().BeNull();
        summary.ReworkRate.Should().Be(0.0);
    }

    [Fact]
    public void ComputeSummary_AvgRegressionCyclesWhenPresent_AveragesOverRegressedOnly()
    {
        // test_intervals=2 → 1 цикл регрессии; test_intervals=4 → 3 цикла.
        // avg = (1 + 3) / 2 = 2.
        var raw = BuildRaw(
            closedReports: new[]
            {
                MakeReport(reportId: 1, testIntervals: 1, fixIntervals: 0), // не считается
                MakeReport(reportId: 2, testIntervals: 2, fixIntervals: 1),
                MakeReport(reportId: 3, testIntervals: 4, fixIntervals: 3),
            },
            phaseAggregates: Array.Empty<PhaseAggregateRow>());

        var summary = AnalyticsService.ComputeSummary(Window, raw);

        summary.AvgRegressionCyclesWhenPresent.Should().BeApproximately(2.0, 1e-9);
    }

    [Fact]
    public void ComputeSummary_FixPhase_ConditionalDenominator_ExcludesReportsWithoutFix()
    {
        // Conditional denominator: знаменатель — только репорты, прошедшие фазу.
        // Fix bucket: 2 репорта, 2 дня → avg_fix = 1.0; при знаменателе 3 (все closed) вышло бы 0.666…
        var raw = BuildRaw(
            closedReports: new[]
            {
                MakeReport(reportId: 1, testIntervals: 1, fixIntervals: 0), // без Fix
                MakeReport(reportId: 2, testIntervals: 2, fixIntervals: 1),
                MakeReport(reportId: 3, testIntervals: 2, fixIntervals: 1),
            },
            phaseAggregates: new[]
            {
                new PhaseAggregateRow
                {
                    Bucket = PhaseBucket.Fix,
                    ReportCount = 2,
                    TotalDurationSeconds = 2 * 86400,
                },
            });

        var summary = AnalyticsService.ComputeSummary(Window, raw);

        summary.AvgFixDays.Should().BeApproximately(1.0, 1e-9);
        // conditional denom не должен подменять основной count.
        summary.ReportsClosed.Should().Be(3);
    }

    [Fact]
    public void ComputeSummary_TestRetest_NullWhenNoRetestReports()
    {
        var raw = BuildRaw(
            closedReports: new[] { MakeReport(reportId: 1, testIntervals: 1, fixIntervals: 0) },
            phaseAggregates: new[]
            {
                new PhaseAggregateRow
                {
                    Bucket = PhaseBucket.TestInitial,
                    ReportCount = 1,
                    TotalDurationSeconds = 86400,
                },
            });

        var summary = AnalyticsService.ComputeSummary(Window, raw);

        summary.AvgTestInitialDays.Should().BeApproximately(1.0, 1e-9);
        summary.AvgTestRetestDays.Should().BeNull();
    }

    [Fact]
    public void ComputeSummary_PhaseTimeDistribution_NormalizesToBoth()
    {
        // Total test = 3 days, Total fix = 1 day → test_pct = 0.75, fix_pct = 0.25.
        var raw = BuildRaw(
            closedReports: new[] { MakeReport(reportId: 1, testIntervals: 1, fixIntervals: 1) },
            phaseAggregates: new[]
            {
                new PhaseAggregateRow
                {
                    Bucket = PhaseBucket.TestInitial,
                    ReportCount = 1,
                    TotalDurationSeconds = 3 * 86400,
                },
                new PhaseAggregateRow
                {
                    Bucket = PhaseBucket.Fix,
                    ReportCount = 1,
                    TotalDurationSeconds = 1 * 86400,
                },
            });

        var summary = AnalyticsService.ComputeSummary(Window, raw);

        summary.TestPct.Should().BeApproximately(0.75, 1e-9);
        summary.FixPct.Should().BeApproximately(0.25, 1e-9);
        (summary.TestPct + summary.FixPct).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void ComputeSummary_TopRegressionReports_EmptyArrayNotNull()
    {
        var raw = BuildRaw(
            closedReports: new[] { MakeReport(reportId: 1, testIntervals: 1, fixIntervals: 0) },
            phaseAggregates: Array.Empty<PhaseAggregateRow>());

        var summary = AnalyticsService.ComputeSummary(Window, raw);

        summary.TopRegressionReports.Should().NotBeNull();
        summary.TopRegressionReports.Should().BeEmpty();
    }

    [Fact]
    public void ComputeSummary_TopRegressionReports_OrderedDescByCyclesAndTakesTen()
    {
        // test_intervals от 2 до 13 → top-10 c regression_cycles от 12 до 3.
        var reports = Enumerable.Range(2, 12)
            .Select(i => MakeReport(reportId: i, testIntervals: i, fixIntervals: i - 1))
            .ToArray();

        var summary = AnalyticsService.ComputeSummary(Window, BuildRaw(reports, Array.Empty<PhaseAggregateRow>()));

        summary.TopRegressionReports.Should().HaveCount(10);
        summary.TopRegressionReports[0].RegressionCycles.Should().Be(12);
        summary.TopRegressionReports[^1].RegressionCycles.Should().Be(3);
    }

    [Fact]
    public void ComputeSummary_PhaseTrendsWeekly_PassedThroughIncludingYearEndIsoWeek()
    {
        // Конец декабря может попасть в ISO-неделю следующего года (2024-12-30 → 2025-W01); это делает postgres,
        // а ComputeSummary обязан пропустить строки насквозь без переинтерпретации.
        var trends = new[]
        {
            new PhaseTrendWeeklyBo { IsoWeek = "2025-W01", TestDays = 1.5, FixDays = 0.5, ReportsClosed = 2 },
            new PhaseTrendWeeklyBo { IsoWeek = "2026-W52", TestDays = 3.0, FixDays = 1.0, ReportsClosed = 4 },
        };

        var raw = new AnalyticsRawData
        {
            ClosedReports = Array.Empty<ClosedReportRow>(),
            PhaseAggregates = Array.Empty<PhaseAggregateRow>(),
            PhaseTrendsWeekly = trends,
        };

        var summary = AnalyticsService.ComputeSummary(Window, raw);

        summary.PhaseTrendsWeekly.Should().HaveCount(2);
        summary.PhaseTrendsWeekly[0].IsoWeek.Should().Be("2025-W01");
        summary.PhaseTrendsWeekly[1].IsoWeek.Should().Be("2026-W52");
        summary.PhaseTrendsWeekly[1].TestDays.Should().Be(3.0);
        summary.PhaseTrendsWeekly[1].ReportsClosed.Should().Be(4);
    }

    [Fact]
    public void ComputeSummary_ReworkRate_EmptyWindow_DoesNotDivideByZero()
    {
        // Регресс-чек: rework_rate на пустом множестве — 0, не NaN/Infinity.
        var raw = new AnalyticsRawData
        {
            ClosedReports = Array.Empty<ClosedReportRow>(),
            PhaseAggregates = Array.Empty<PhaseAggregateRow>(),
            PhaseTrendsWeekly = Array.Empty<PhaseTrendWeeklyBo>(),
        };

        var summary = AnalyticsService.ComputeSummary(Window, raw);

        summary.ReworkRate.Should().Be(0.0);
        double.IsNaN(summary.ReworkRate).Should().BeFalse();
        double.IsInfinity(summary.ReworkRate).Should().BeFalse();
    }

    [Fact]
    public void ComputeSummary_SingleReportWithoutRegression_AvgRegressionCyclesIsNull()
    {
        // Без регрессий avg_regression_cycles_when_present — null (нет данных), не 0.
        var raw = BuildRaw(
            closedReports: new[] { MakeReport(reportId: 1, testIntervals: 1, fixIntervals: 1) },
            phaseAggregates: Array.Empty<PhaseAggregateRow>());

        var summary = AnalyticsService.ComputeSummary(Window, raw);

        summary.ReportsClosed.Should().Be(1);
        summary.ReworkRate.Should().Be(0.0);
        summary.AvgRegressionCyclesWhenPresent.Should().BeNull();
    }

    [Fact]
    public void ComputeSummary_AvgFullCycle_SkipsReportsWithoutFirstTest()
    {
        // r1: closed_at − first_test_entered_at = 1 day; r2 без Test-фазы пропускается → avg = 1.0.
        var t0 = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        var raw = BuildRaw(
            closedReports: new[]
            {
                new ClosedReportRow
                {
                    ReportId = 1, Title = "r1",
                    CreatedAt = t0,
                    FirstTestEnteredAt = t0.AddHours(6),
                    ClosedAt = t0.AddHours(30),
                    TestIntervals = 1, FixIntervals = 0,
                    TestDurationSeconds = 0, FixDurationSeconds = 0,
                },
                new ClosedReportRow
                {
                    ReportId = 2, Title = "r2",
                    CreatedAt = t0,
                    FirstTestEnteredAt = null,
                    ClosedAt = t0.AddDays(7),
                    TestIntervals = 0, FixIntervals = 0,
                    TestDurationSeconds = 0, FixDurationSeconds = 0,
                },
            },
            phaseAggregates: Array.Empty<PhaseAggregateRow>());

        var summary = AnalyticsService.ComputeSummary(Window, raw);

        summary.AvgFullCycleDays.Should().BeApproximately(1.0, 1e-9);
    }


    private static ClosedReportRow MakeReport(int reportId, int testIntervals, int fixIntervals)
    {
        var t0 = new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero);
        return new ClosedReportRow
        {
            ReportId = reportId,
            Title = $"r{reportId}",
            CreatedAt = t0,
            FirstTestEnteredAt = testIntervals > 0 ? t0.AddHours(1) : null,
            ClosedAt = t0.AddDays(1),
            TestIntervals = testIntervals,
            FixIntervals = fixIntervals,
            TestDurationSeconds = 0,
            FixDurationSeconds = 0,
        };
    }

    private static AnalyticsRawData BuildRaw(
        IReadOnlyList<ClosedReportRow> closedReports,
        IReadOnlyList<PhaseAggregateRow> phaseAggregates) => new()
        {
            ClosedReports = closedReports,
            PhaseAggregates = phaseAggregates,
            PhaseTrendsWeekly = Array.Empty<PhaseTrendWeeklyBo>(),
        };
}
