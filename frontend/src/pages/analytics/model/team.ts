import { createEffect, createEvent, createStore, sample } from "effector";

import { analyticsApi, type AnalyticsSummary } from "@/shared/api";
import { type AnalyticsPeriod, defaultPeriod } from "@/shared/lib/time";

/**
 * Effector-модель Разреза 3 (team-уровень).
 *
 * - `$teamIdStore` — выбранный team (источник истины — URL ?team=...).
 * - `$periodStore` — выбранный период.
 * - `$summaryStore` — последний успешный ответ /v2/analytics/summary?teamId=...
 *   (после R6 единый summary-эндпоинт с опциональным фильтром по команде).
 * - `fetchTeamSummaryFx` — запрос сводки.
 *
 * Любое изменение teamId/period перезапускает fetch (если виджет смонтирован).
 */

export const periodChanged = createEvent<AnalyticsPeriod>();
export const teamIdChanged = createEvent<string | null>();
export const teamMounted = createEvent();
export const teamUnmounted = createEvent();

export const fetchTeamSummaryFx = createEffect<
  { teamId: string; period: AnalyticsPeriod },
  AnalyticsSummary
>(async ({ teamId, period }) =>
  analyticsApi.getAnalyticsSummary(period, teamId)
);

export const $periodStore = createStore<AnalyticsPeriod>(defaultPeriod).on(
  periodChanged,
  (_, p) => p
);

export const $teamIdStore = createStore<string | null>(null).on(
  teamIdChanged,
  (_, id) => id
);

export const $summaryStore = createStore<AnalyticsSummary | null>(null)
  .on(fetchTeamSummaryFx.doneData, (_, data) => data)
  .reset(teamIdChanged);

export const $summaryError = createStore<string | null>(null)
  .on(
    fetchTeamSummaryFx.failData,
    (_, err) => err?.message ?? "Ошибка загрузки"
  )
  .reset(fetchTeamSummaryFx.doneData)
  .reset(teamIdChanged);

const $isMounted = createStore(false)
  .on(teamMounted, () => true)
  .on(teamUnmounted, () => false);

// Запрашиваем сводку, только когда есть выбранный team и виджет смонтирован.
sample({
  clock: [teamMounted, periodChanged, teamIdChanged],
  source: { teamId: $teamIdStore, period: $periodStore, mounted: $isMounted },
  filter: ({ teamId, mounted }) => Boolean(teamId) && mounted,
  fn: ({ teamId, period }) => ({ teamId: teamId as string, period }),
  target: fetchTeamSummaryFx,
});
