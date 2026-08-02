import * as reportsApi from "@/shared/api/reports";
import type { components as analyticsComponents } from "@/shared/api/generated/analytics";
import type { AnalyticsPeriod } from "@/shared/lib/time";
import { request } from "./client";
import type { Result } from "./client";

/**
 * Ручки аналитики.
 *
 * Контракт resource-oriented:
 *   * `GET /v2/analytics/summary?period=...&teamId=...` — единый summary
 *     (workspace-wide или фильтр по команде);
 *   * `GET /v2/analytics/responsible/{userId}?period=...` — отдельный shape;
 *   * `GET /v2/reports/{id}/analytics` — sub-resource модуля `reports`.
 *
 * Тела ответов приходят с провода в `snake_case` и перекладываются в `camelCase`
 * общим интерсептором (`shared/api/instances/base.ts`) — как во всех остальных
 * HTTP-модулях, без URL-исключений (ADR-0009). Имена query-параметров конверсию
 * не проходят и берутся из контракта.
 */

/* ── Сводка ────────────────────────────────────────────────────────────────── */

export type AnalyticsSummary = Result<"/v2/analytics/summary", "get">;

/**
 * `teamId` уходит в адрес только непустым — ровно как раньше, когда call-site
 * выбирал между `{ period, teamId }` и `{ period }`: пустой фильтр не должен
 * превращаться в `teamId=` на проводе.
 */
export const getAnalyticsSummary = (
  period: AnalyticsPeriod,
  teamId?: string | null
): Promise<AnalyticsSummary> =>
  request("/v2/analytics/summary", "get", {
    query: { period, teamId: teamId || undefined },
  });

/* ── Сводка по ответственному ──────────────────────────────────────────────── */

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

/* ── Detail по репорту ─────────────────────────────────────────────────────── */

/**
 * Sub-resource модуля `reports`, поэтому зовётся его операцией, а не собственным
 * путём: у каждого пути ровно одна транспортная граница.
 */
export const getReportAnalytics = (
  reportId: string
): Promise<AnalyticsReport> => reportsApi.getReportAnalytics(reportId);

/* ── Формы ответов ─────────────────────────────────────────────────────────── */

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

// Значения enum-подобных схем — данные, а не имена полей: конверсия их не трогает,
// и своей операции у них нет, поэтому имя берётся из схемы контракта напрямую.
export type ResponsibleOutcome =
  analyticsComponents["schemas"]["ResponsibleOutcome"];

/** Detail-формы описаны в контракте модуля `reports` — оттуда и берутся. */
export type AnalyticsReport = reportsApi.ReportAnalyticsResult;
export type AnalyticsReportPhaseEntry =
  AnalyticsReport["phaseTimeline"][number];
export type AnalyticsReportBugsByStatus = AnalyticsReport["bugsByStatus"];
export type PhaseName = AnalyticsReportPhaseEntry["phase"];
