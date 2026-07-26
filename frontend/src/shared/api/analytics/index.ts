import { appApi } from "@/shared/api/instances";
import type { components as analyticsComponents } from "@/shared/api/generated/analytics";
import type { components as reportsComponents } from "@/shared/api/generated/reports";
import type { AnalyticsPeriod } from "@/shared/lib/time";

/**
 * HTTP-клиенты для analytics-эндпоинтов.
 *
 * Wire-формат: snake_case в обе стороны. Case-conversion интерсептор
 * в `shared/api/instances/base.ts` точечно отключён для `/v2/analytics/*`,
 * поэтому возвращаемые DTO здесь совпадают с типами из generated/analytics.d.ts.
 *
 * После R6 контракт перешёл на resource-oriented форму:
 *   * `GET /v2/analytics/summary?period=...&teamId=...` — единый summary
 *     (workspace-wide или фильтр по команде).
 *   * `GET /v2/reports/{id}/analytics` — sub-resource (detail по репорту).
 *   * `GET /v2/analytics/responsible/{userId}?period=...` — отдельный shape.
 *
 * Detail-DTO `AnalyticsReport*` теперь живут в модуле `reports` (см. контракт);
 * frontend это не волнует, мы просто реэкспортируем алиасы.
 */

export type AnalyticsSummary =
  analyticsComponents["schemas"]["AnalyticsSummary"];
export type AnalyticsResponsible =
  analyticsComponents["schemas"]["AnalyticsResponsible"];
export type AnalyticsResponsibleParticipatedReport =
  analyticsComponents["schemas"]["AnalyticsResponsibleParticipatedReport"];
export type AnalyticsResponsibleCompletedReport =
  analyticsComponents["schemas"]["AnalyticsResponsibleCompletedReport"];
export type Period = analyticsComponents["schemas"]["Period"];
export type ResponsibleOutcome =
  analyticsComponents["schemas"]["ResponsibleOutcome"];
export type AvgPhaseDurationDays =
  analyticsComponents["schemas"]["AvgPhaseDurationDays"];
export type PhaseTimeDistribution =
  analyticsComponents["schemas"]["PhaseTimeDistribution"];
export type TopRegressionReport =
  analyticsComponents["schemas"]["TopRegressionReport"];
export type PhaseTrendWeekly =
  analyticsComponents["schemas"]["PhaseTrendWeekly"];

// Detail-DTO переехали в модуль reports; алиасим оттуда, чтобы виджеты могли
// продолжать импортировать их из @/shared/api без знания о расщеплении контракта.
export type AnalyticsReport = reportsComponents["schemas"]["AnalyticsReport"];
export type AnalyticsReportPhaseEntry =
  reportsComponents["schemas"]["AnalyticsReportPhaseEntry"];
export type AnalyticsReportBugsByStatus =
  reportsComponents["schemas"]["AnalyticsReportBugsByStatus"];
export type PhaseName = reportsComponents["schemas"]["PhaseName"];

export const getAnalyticsSummary = async (
  period: AnalyticsPeriod,
  teamId?: string | null
): Promise<AnalyticsSummary> => {
  const { data } = await appApi.get<AnalyticsSummary>("/v2/analytics/summary", {
    params: teamId ? { period, teamId } : { period },
  });
  return data;
};

export const getReportAnalytics = async (
  reportId: number
): Promise<AnalyticsReport> => {
  const { data } = await appApi.get<AnalyticsReport>(
    `/v2/reports/${reportId}/analytics`
  );
  return data;
};

export const getAnalyticsByResponsible = async (
  userId: string,
  period: AnalyticsPeriod
): Promise<AnalyticsResponsible> => {
  const { data } = await appApi.get<AnalyticsResponsible>(
    `/v2/analytics/responsible/${userId}`,
    { params: { period } }
  );
  return data;
};
