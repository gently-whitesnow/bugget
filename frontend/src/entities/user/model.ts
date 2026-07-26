import type { CurrentUserResponse, ExternalLink } from "./api/contracts";
import { createEffect, createEvent, createStore, sample } from "effector";
import {
  fetchCurrentUser,
  fetchExternalLinks,
  logout,
  mergeAccounts,
  unlinkProvider,
} from "./api";

// Унифицированный тип для текущего пользователя
export type UserStoreModel = {
  id: string;
  name?: string;
  imageUrl?: string | null;
  workspaceRole?: string | null;
  mattermostUserId?: string | null;
};

// Выход из системы
export const logoutFx = createEffect(async () => {
  return await logout();
});

// Self-hosted/SaaS: получение текущего пользователя через users-api
export const fetchCurrentUserFx = createEffect<
  { workspaceId?: string | number; teamId?: string | number },
  CurrentUserResponse
>(async ({ workspaceId, teamId }) => {
  return await fetchCurrentUser(workspaceId, teamId);
});

export const $authUserStore = createStore<UserStoreModel>({} as UserStoreModel)
  .on(fetchCurrentUserFx.doneData, (_, user) => ({
    id: user.id,
    name: user.name,
    imageUrl: user.imageUrl,
    workspaceRole: user.workspaceRole,
    mattermostUserId: user.mattermostUserId,
  }))
  .reset(logoutFx.done);

// Автозагрузка при первой подписке (опционально)
export const loadUserEvent = createEvent<{
  workspaceId: string | number;
  teamId: string | number;
}>();
sample({
  clock: loadUserEvent,
  target: fetchCurrentUserFx,
});

// External links (привязанные провайдеры)
export const fetchExternalLinksFx = createEffect<void, ExternalLink[]>(
  async () => {
    return await fetchExternalLinks();
  }
);

export const unlinkProviderFx = createEffect<string, void>(async (provider) => {
  await unlinkProvider(provider);
});

export const $externalLinksStore = createStore<ExternalLink[]>([])
  .on(fetchExternalLinksFx.doneData, (_, links) => links)
  .reset(logoutFx.done);

export const mergeAccountsFx = createEffect<string, void>(
  async (sourceUserId) => {
    await mergeAccounts(sourceUserId);
  }
);

sample({
  clock: unlinkProviderFx.done,
  target: fetchExternalLinksFx,
});

sample({
  clock: mergeAccountsFx.done,
  target: fetchExternalLinksFx,
});

// Экспорт функции для запуска автофетча
export function ensureUserLoaded(
  workspaceId: string | number,
  teamId: string | number
) {
  loadUserEvent({ workspaceId, teamId });
}
