import { describe, expect, it } from "vitest";
import { convertObjectToCamel } from "@/shared/lib/convertCases";
import type { components as analyticsComponents } from "@/shared/api/generated/analytics";
import type { components as reportsComponents } from "@/shared/api/generated/reports";
import type { AnalyticsReport, AnalyticsSummary } from "./index";

/**
 * Analytics больше не исключение из case-границы: тела ответов приходят с провода
 * в `snake_case` и перекладываются интерсептором в `camelCase`, а типы фронта
 * выводятся из сгенерированных схем через `Camelized<T>`.
 *
 * Здесь сверяются обе половины обещания: рантайм (конверсия доходит до дна) и
 * типы (то, что получилось, присваивается экспортируемому типу модуля — рукописный
 * DTO для этого не заводится).
 */

const collectKeys = (value: unknown, acc: string[] = []): string[] => {
  if (value === null || typeof value !== "object") return acc;

  if (Array.isArray(value)) {
    value.forEach((item) => collectKeys(item, acc));
    return acc;
  }

  for (const [key, nested] of Object.entries(
    value as Record<string, unknown>
  )) {
    acc.push(key);
    collectKeys(nested, acc);
  }

  return acc;
};

const wireSummary: analyticsComponents["schemas"]["AnalyticsSummary"] = {
  period: {
    from: "2026-07-01T00:00:00Z",
    to: "2026-07-30T00:00:00Z",
    label: "Июль",
  },
  avg_phase_duration_days: { test_initial: 2.5, test_retest: null, fix: 1 },
  avg_full_cycle_days: null,
  rework_rate: 0.25,
  avg_regression_cycles_when_present: null,
  reports_closed: 4,
  phase_time_distribution: { test_pct: 0.6, fix_pct: 0.4 },
  top_regression_reports: [
    { report_id: 12, title: "Падает карточка", regression_cycles: 2 },
  ],
  phase_trends_weekly: [
    { iso_week: "2026-W30", test_days: 1.5, fix_days: 0.5, reports_closed: 2 },
  ],
};

const wireReport: reportsComponents["schemas"]["AnalyticsReport"] = {
  report_id: 12,
  phase_timeline: [
    {
      phase: "Test",
      entered_at: "2026-07-01T10:00:00Z",
      exited_at: null,
      duration_days: null,
      regression_cycle_index: 0,
    },
  ],
  regression_cycles: 2,
  bugs_by_status: { open: 1, fixed: 2, verified: 3, rejected: 0 },
  bugs_added_during_regression: 1,
};

describe("analytics на общей case-границе", () => {
  it("конверсия доходит до дна: snake_case ключей в ответе не остаётся", () => {
    const summary = convertObjectToCamel(wireSummary);
    const report = convertObjectToCamel(wireReport);

    expect(
      [...collectKeys(summary), ...collectKeys(report)].filter((key) =>
        key.includes("_")
      )
    ).toEqual([]);
  });

  it("camelCase-форма присваивается типу модуля — второго DTO не появляется", () => {
    const summary = convertObjectToCamel(wireSummary) as AnalyticsSummary;
    const report = convertObjectToCamel(wireReport) as AnalyticsReport;

    expect(summary.avgPhaseDurationDays.testInitial).toBe(2.5);
    expect(summary.avgPhaseDurationDays.testRetest).toBeNull();
    expect(summary.phaseTimeDistribution.testPct).toBe(0.6);
    expect(summary.topRegressionReports[0].reportId).toBe(12);
    expect(summary.phaseTrendsWeekly[0].isoWeek).toBe("2026-W30");
    // Значения enum-подобных схем — данные, конверсия их не трогает.
    expect(report.phaseTimeline[0].phase).toBe("Test");
    expect(report.phaseTimeline[0].exitedAt).toBeNull();
    expect(report.bugsByStatus.verified).toBe(3);
    expect(report.bugsAddedDuringRegression).toBe(1);
  });
});
