import { createEffect, createEvent, createStore, sample } from "effector";

import { analyticsApi, type AnalyticsSummary } from "@/shared/api";
import { type AnalyticsPeriod, defaultPeriod } from "@/shared/lib/time";

/**
 * Разрез 1 (workspace-уровень). Источник истины периода — URL ?period=...,
 * page прокидывает его через `periodChanged`; смена периода даёт новый fetch.
 */

export const periodChanged = createEvent<AnalyticsPeriod>();
export const overviewMounted = createEvent();
export const overviewUnmounted = createEvent();

export const fetchSummaryFx = createEffect<AnalyticsPeriod, AnalyticsSummary>(
  async (period) => analyticsApi.getAnalyticsSummary(period)
);

export const $periodStore = createStore<AnalyticsPeriod>(defaultPeriod).on(
  periodChanged,
  (_, p) => p
);

export const $summaryStore = createStore<AnalyticsSummary | null>(null).on(
  fetchSummaryFx.doneData,
  (_, data) => data
);

export const $summaryError = createStore<string | null>(null)
  .on(fetchSummaryFx.failData, (_, err) => err?.message ?? "Ошибка загрузки")
  .reset(fetchSummaryFx.doneData);

const $isOverviewMounted = createStore(false)
  .on(overviewMounted, () => true)
  .on(overviewUnmounted, () => false);

sample({
  clock: overviewMounted,
  source: $periodStore,
  target: fetchSummaryFx,
});

// Смена периода (только когда секция активна) → новый fetch.
sample({
  clock: periodChanged,
  source: $isOverviewMounted,
  filter: (mounted) => mounted,
  fn: (_, period) => period,
  target: fetchSummaryFx,
});
