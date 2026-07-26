import { createEffect, createEvent, createStore, sample } from "effector";

import { analyticsApi, type AnalyticsReport } from "@/shared/api";

/**
 * Effector-модель Разреза 2 (детализация по конкретному репорту).
 *
 * - `$reportIdStore` — выбранный reportId (источник истины — URL ?report=...).
 *   `null` означает «репорт не выбран».
 * - `$reportStore` — последний успешный ответ /v2/reports/{id}/analytics
 *   (sub-resource на репорте, после R6).
 * - `fetchReportFx` — запрос детальной аналитики.
 *
 * Любое изменение `reportId` (на ненулевое) при смонтированном виджете
 * перезапускает fetch. При сбросе reportId в null — стор обнуляется.
 */

export const reportIdChanged = createEvent<number | null>();
export const reportMounted = createEvent();
export const reportUnmounted = createEvent();

export const fetchReportFx = createEffect<number, AnalyticsReport>(
  async (reportId) => analyticsApi.getReportAnalytics(reportId)
);

export const $reportIdStore = createStore<number | null>(null).on(
  reportIdChanged,
  (_, id) => id
);

export const $reportStore = createStore<AnalyticsReport | null>(null)
  .on(fetchReportFx.doneData, (_, data) => data)
  .reset(reportIdChanged);

export const $reportError = createStore<string | null>(null)
  .on(fetchReportFx.failData, (_, err) => err?.message ?? "Ошибка загрузки")
  .reset(fetchReportFx.doneData)
  .reset(reportIdChanged);

const $isMounted = createStore(false)
  .on(reportMounted, () => true)
  .on(reportUnmounted, () => false);

// Запрашиваем аналитику, только когда есть выбранный reportId и виджет смонтирован.
sample({
  clock: [reportMounted, reportIdChanged],
  source: { reportId: $reportIdStore, mounted: $isMounted },
  filter: ({ reportId, mounted }) =>
    typeof reportId === "number" && Number.isFinite(reportId) && mounted,
  fn: ({ reportId }) => reportId as number,
  target: fetchReportFx,
});
