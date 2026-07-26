import { createEvent, createStore } from "effector";

// События
export const dashboardPageOpened = createEvent();
export const showDashboard = createEvent();
export const hideDashboard = createEvent();
export const teamPageOpened = createEvent<string>();
export const setRecentlyResolvedSectionOpened = createEvent<boolean>();

// Сторы
export const $isDashboardVisible = createStore<boolean>(true)
  .on(showDashboard, () => true)
  .on(hideDashboard, () => false)
  .on(teamPageOpened, () => false);

export const $isRecentlyResolvedSectionOpenStore = createStore<boolean>(
  false
).on(setRecentlyResolvedSectionOpened, (_, isOpen) => isOpen);
