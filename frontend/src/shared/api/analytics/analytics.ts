import * as reportsApi from "@/shared/api/reports";
import type { components as analyticsComponents } from "@/shared/api/generated/analytics";
import type { AnalyticsPeriod } from "@/shared/lib/time";
import { request } from "./client";
import type { Result } from "./client";

/**
 * Ручки аналитики. Тела ответов перекладывает в camelCase общий интерсептор;
 * имена query конверсию не проходят и берутся из контракта (ADR-0009).
 */

export type AnalyticsSummary = Result<"/v2/analytics/summary", "get">;

/** Пустой `teamId` в адрес не уходит: на проводе не должно быть `teamId=`. */
export const getAnalyticsSummary = (
  period: AnalyticsPeriod,
  teamId?: string | null
): Promise<AnalyticsSummary> =>
  request("/v2/analytics/summary", "get", {
    query: { period, teamId: teamId || undefined },
  });

export type AnalyticsResponsible = Result<
  "/v2/analytics/responsible/{userId}",
  "get"
>;

export const getAnalyticsByResponsible = (
  userId: string,
  period: AnalyticsPeriod
): Promise<AnalyticsResponsible> =>
  request("/v2/analytics/responsible/{userId}", "get", {
    path: { userId },
    query: { period },
  });

/** Sub-resource модуля `reports` — зовётся его операцией, не своим путём. */
export const getReportAnalytics = (
  reportId: string
): Promise<AnalyticsReport> => reportsApi.getReportAnalytics(reportId);

export type Period = AnalyticsSummary["period"];
export type AvgPhaseDurationDays = AnalyticsSummary["avgPhaseDurationDays"];
export type PhaseTimeDistribution = AnalyticsSummary["phaseTimeDistribution"];
export type TopRegressionReport =
  AnalyticsSummary["topRegressionReports"][number];
export type PhaseTrendWeekly = AnalyticsSummary["phaseTrendsWeekly"][number];

export type AnalyticsResponsibleParticipatedReport =
  AnalyticsResponsible["reportsParticipated"][number];
export type AnalyticsResponsibleCompletedReport =
  AnalyticsResponsible["reportsCompleted"][number];

// Значения enum-схем — данные, не имена полей: конверсия их не трогает, своей
// операции у них нет — имя берётся из схемы контракта напрямую.
export type ResponsibleOutcome =
  analyticsComponents["schemas"]["ResponsibleOutcome"];

/** Detail-формы описаны в контракте модуля `reports` — оттуда и берутся. */
export type AnalyticsReport = reportsApi.ReportAnalyticsResult;
export type AnalyticsReportPhaseEntry =
  AnalyticsReport["phaseTimeline"][number];
export type AnalyticsReportBugsByStatus = AnalyticsReport["bugsByStatus"];
export type PhaseName = AnalyticsReportPhaseEntry["phase"];
