import { createEffect, createEvent, createStore, sample } from "effector";

import {
  analyticsApi,
  fetchUsers,
  type AnalyticsResponsible,
  type UserResponse,
} from "@/shared/api";
import { type AnalyticsPeriod, defaultPeriod } from "@/shared/lib/time";

/**
 * Effector-модель Разреза 4 (ответственный).
 *
 * - `$userIdStore` — выбранный пользователь (источник — URL ?user=...).
 * - `$periodStore` — выбранный период.
 * - `$selectedUserPreview` — id/name/imageUrl для рендера в Autosuggest:
 *   заполняется при выборе из дропдауна, либо одноразовым `fetchUsers`
 *   при восстановлении состояния из URL.
 * - `$responsibleStore` — последний успешный ответ
 *   /v2/analytics/responsible/{userId}.
 */

export type SelectedUserPreview = {
  id: string;
  name: string;
  imageUrl?: string;
};

export const periodChanged = createEvent<AnalyticsPeriod>();
export const userIdChanged = createEvent<string | null>();
export const userSelected = createEvent<SelectedUserPreview | null>();
export const responsibleMounted = createEvent();
export const responsibleUnmounted = createEvent();

export const fetchResponsibleFx = createEffect<
  { userId: string; period: AnalyticsPeriod },
  AnalyticsResponsible
>(async ({ userId, period }) =>
  analyticsApi.getAnalyticsByResponsible(userId, period)
);

const fetchSelectedUserFx = createEffect<string, UserResponse[]>(
  async (userId) => fetchUsers([userId])
);

export const $periodStore = createStore<AnalyticsPeriod>(defaultPeriod).on(
  periodChanged,
  (_, p) => p
);

export const $userIdStore = createStore<string | null>(null).on(
  userIdChanged,
  (_, id) => id
);

export const $selectedUserPreview = createStore<SelectedUserPreview | null>(
  null
)
  .on(userSelected, (_, preview) => preview)
  .on(fetchSelectedUserFx.doneData, (_, users) => {
    const u = users[0];
    if (!u) return null;
    return { id: u.id, name: u.name, imageUrl: u.imageUrl ?? undefined };
  });

export const $responsibleStore = createStore<AnalyticsResponsible | null>(null)
  .on(fetchResponsibleFx.doneData, (_, data) => data)
  .reset(userIdChanged);

export const $responsibleError = createStore<string | null>(null)
  .on(
    fetchResponsibleFx.failData,
    (_, err) => err?.message ?? "Ошибка загрузки"
  )
  .reset(fetchResponsibleFx.doneData)
  .reset(userIdChanged);

const $isMounted = createStore(false)
  .on(responsibleMounted, () => true)
  .on(responsibleUnmounted, () => false);

// Запрашиваем разрез, только когда есть выбранный user и виджет смонтирован.
sample({
  clock: [responsibleMounted, periodChanged, userIdChanged],
  source: { userId: $userIdStore, period: $periodStore, mounted: $isMounted },
  filter: ({ userId, mounted }) => Boolean(userId) && mounted,
  fn: ({ userId, period }) => ({ userId: userId as string, period }),
  target: fetchResponsibleFx,
});

// Восстановление из URL: если userId пришёл, а превью пустое или с другим id —
// одноразово подтянем имя/аватар, чтобы Autosuggest показал выбранного.
sample({
  clock: userIdChanged,
  source: $selectedUserPreview,
  filter: (preview, userId) =>
    Boolean(userId) && (preview === null || preview.id !== userId),
  fn: (_, userId) => userId as string,
  target: fetchSelectedUserFx,
});
