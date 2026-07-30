import { appApi } from "@/shared/api/instances";
import type { components as analyticsComponents } from "@/shared/api/generated/analytics";
import type { components as reportsComponents } from "@/shared/api/generated/reports";
import type { Camelized } from "@/shared/lib/types";
import type { AnalyticsPeriod } from "@/shared/lib/time";

/**
 * HTTP-клиенты для analytics-эндпоинтов.
 *
 * Тела ответов приходят с провода в `snake_case` и перекладываются в `camelCase`
 * общим интерсептором (`shared/api/instances/base.ts`) — как во всех остальных
 * HTTP-модулях, без URL-исключений. Поэтому типы ответов выводятся из
 * сгенерированных схем через `Camelized<T>`, а не дублируются рукописным DTO.
 *
 * Query-параметры (`period`, `teamId`) и сегменты пути конверсию не проходят:
 * их имена — часть публичного контракта и берутся из generated напрямую.
 *
 * Контракт resource-oriented:
 *   * `GET /v2/analytics/summary?period=...&teamId=...` — единый summary
 *     (workspace-wide или фильтр по команде).
 *   * `GET /v2/reports/{id}/analytics` — sub-resource (detail по репорту).
 *   * `GET /v2/analytics/responsible/{userId}?period=...` — отдельный shape.
 *
 * Detail-DTO `AnalyticsReport*` живут в модуле `reports` (см. контракт);
 * frontend это не волнует, мы просто реэкспортируем алиасы.
 */

type AnalyticsSchemas = analyticsComponents["schemas"];
type ReportsSchemas = reportsComponents["schemas"];

export type AnalyticsSummary = Camelized<AnalyticsSchemas["AnalyticsSummary"]>;
export type AnalyticsResponsible = Camelized<
  AnalyticsSchemas["AnalyticsResponsible"]
>;
export type AnalyticsResponsibleParticipatedReport = Camelized<
  AnalyticsSchemas["AnalyticsResponsibleParticipatedReport"]
>;
export type AnalyticsResponsibleCompletedReport = Camelized<
  AnalyticsSchemas["AnalyticsResponsibleCompletedReport"]
>;
export type AvgPhaseDurationDays = Camelized<
  AnalyticsSchemas["AvgPhaseDurationDays"]
>;
export type PhaseTimeDistribution = Camelized<
  AnalyticsSchemas["PhaseTimeDistribution"]
>;
export type TopRegressionReport = Camelized<
  AnalyticsSchemas["TopRegressionReport"]
>;
export type PhaseTrendWeekly = Camelized<AnalyticsSchemas["PhaseTrendWeekly"]>;

export type Period = Camelized<AnalyticsSchemas["Period"]>;

// Значения enum-подобных схем — данные, а не имена полей: конверсия их не трогает.
export type ResponsibleOutcome = AnalyticsSchemas["ResponsibleOutcome"];

export type AnalyticsReport = Camelized<ReportsSchemas["AnalyticsReport"]>;
export type AnalyticsReportPhaseEntry = Camelized<
  ReportsSchemas["AnalyticsReportPhaseEntry"]
>;
export type AnalyticsReportBugsByStatus = Camelized<
  ReportsSchemas["AnalyticsReportBugsByStatus"]
>;
export type PhaseName = ReportsSchemas["PhaseName"];

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
