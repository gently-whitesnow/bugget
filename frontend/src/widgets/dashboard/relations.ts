import { combine, createEffect, createStore, sample } from "effector";

// Импорты через публичный API других entities
import { fetchReportsList } from "@/entities/report";
import type { ListReportsResponse } from "@/shared/api";
import {
  loadReportsFx,
  fetchReportsUsersFx,
  $reportsStore,
} from "@/entities/report-list";
import { fetchCurrentUserFx, $authUserStore } from "@/entities/user";

import {
  ReportStatuses,
  lastReportsDashboardTake,
  reportStatusOrder,
} from "@/shared/config";

import {
  $isRecentlyResolvedSectionOpenStore,
  dashboardPageOpened,
  setRecentlyResolvedSectionOpened,
  teamPageOpened,
} from "@/entities/dashboard";

// Эффект для загрузки недавно решённых репортов (RESOLVED или REJECTED)
export const loadRecentlyResolvedFx = createEffect(async (userId: string) => {
  const data = await fetchReportsList(
    userId,
    null,
    [ReportStatuses.RESOLVED, ReportStatuses.REJECTED],
    0,
    lastReportsDashboardTake
  );
  return data;
});

// Стор для недавно решённых репортов
export const $recentlyResolvedReports = createStore<ListReportsResponse>({
  total: 0,
  reports: [],
}).on(loadRecentlyResolvedFx.doneData, (_, data) => data);

// Вычисляемые сторы на основе других entities
export const $responsibleReports = combine(
  $reportsStore,
  $authUserStore,
  (data, user) => {
    const reports = data.reports;
    return reports
      .filter((report) => report.responsibleUserId === user?.id)
      .sort(
        (a, b) => reportStatusOrder[b.status] - reportStatusOrder[a.status]
      );
  }
);

export const $participantReports = combine(
  $reportsStore,
  $authUserStore,
  (data, user) => {
    const reports = data.reports;
    return reports
      .filter((report) => report.responsibleUserId !== user?.id)
      .sort(
        (a, b) => reportStatusOrder[b.status] - reportStatusOrder[a.status]
      );
  }
);

// Реакции и связи
const $isDashboardPageActive = createStore<boolean>(false).on(
  dashboardPageOpened,
  () => true
);

sample({
  source: $authUserStore,
  clock: dashboardPageOpened,
  filter: (user) => !!user?.id,
  fn: (user) => ({ userId: user.id }),
  target: loadReportsFx,
});

// Загружаем репорты после получения текущего пользователя
sample({
  clock: fetchCurrentUserFx.doneData,
  source: $isDashboardPageActive,
  filter: (isActive, user) => isActive && !!user?.id,
  fn: (isActive, user) => ({ userId: user.id }),
  target: loadReportsFx,
});

sample({
  clock: teamPageOpened,
  fn: (teamId) => ({ teamId }),
  target: loadReportsFx,
});

sample({
  source: $authUserStore,
  clock: setRecentlyResolvedSectionOpened,
  filter: (user, isOpen) => isOpen && !!user?.id,
  fn: (user) => user.id,
  target: loadRecentlyResolvedFx,
});

// Загружаем недавно решённые после получения текущего пользователя
sample({
  clock: fetchCurrentUserFx.doneData,
  source: $isRecentlyResolvedSectionOpenStore,
  filter: (isOpen, user) => isOpen && !!user?.id,
  fn: (isOpen, user) => user.id,
  target: loadRecentlyResolvedFx,
});

sample({
  clock: loadRecentlyResolvedFx.doneData,
  source: $recentlyResolvedReports,
  fn: (data) => {
    const allIds = new Set<string>();
    data.reports.forEach((report) => {
      allIds.add(report.responsibleUserId);
      allIds.add(report.creatorUserId);
      report.participantsUserIds?.forEach((id) => allIds.add(id));
    });
    return Array.from(allIds).filter(Boolean);
  },
  filter: (data) => data.reports.length > 0,
  target: fetchReportsUsersFx,
});
