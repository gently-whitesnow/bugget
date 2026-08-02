import { createEffect, createStore, sample } from "effector";
import { reportsApi, usersApi } from "@/shared/api";
import { CreatorTypes, ReportStatuses } from "@/shared/config";
import type {
  ListReportsResponse,
  ReportListItem,
  UserResponse,
} from "@/shared/api";

// Формы ответа списка выведены из контракта — см. shared/api/contracts/reports.ts
export type { ListReportsResponse, ReportListItem };

const defaultReportStatuses: ReportStatuses[] = [
  ReportStatuses.BACKLOG,
  ReportStatuses.FIX,
  ReportStatuses.TEST,
];

type LoadReportsParams = {
  userId?: string | null;
  teamId?: string | null;
  statuses?: ReportStatuses[];
  creatorTypes?: CreatorTypes[];
  offset?: number;
  take?: number;
};

// Общий эффект для загрузки репортов
export const loadReportsFx = createEffect<
  LoadReportsParams,
  ListReportsResponse
>(
  async ({
    userId = null,
    teamId = null,
    statuses = defaultReportStatuses,
    creatorTypes,
    offset = 0,
    take,
  }) => {
    // Одна реализация операции LIST на весь фронт — в shared/api/reports.
    // Пустой фильтр по пользователю и команде не отправляется, как и раньше.
    return await reportsApi.listReports({
      userId: userId || undefined,
      teamId: teamId || undefined,
      reportStatuses: statuses,
      creatorTypes,
      skip: offset,
      take,
    });
  }
);

// Эффект для загрузки пользователей репортов
export const fetchReportsUsersFx = createEffect<string[], UserResponse[]>(
  async (userIds) => {
    if (userIds.length === 0) return [];
    return await usersApi.fetchUsers(userIds);
  }
);

// Стор для репортов
// `total` — канон Int64String с провода (`shared/lib/wireInt64`), поэтому и
// пустое состояние держит строку: смешивать формы в одном сторе нечем.
export const $reportsStore = createStore<ListReportsResponse>({
  total: "0",
  reports: [],
}).on(loadReportsFx.doneData, (_, reports) => reports);

// Стор для отслеживания, загружены ли репорты дашборда
export const $isDashboardReportsLoaded = createStore<boolean>(false).on(
  loadReportsFx.doneData,
  () => true
);

// Стор для хранения пользователей по ID
export const $reportsUsersStore = createStore<Record<string, UserResponse>>(
  {}
).on(fetchReportsUsersFx.doneData, (state, users) => {
  const usersById = users.reduce(
    (acc, user) => {
      acc[user.id] = user;
      return acc;
    },
    {} as Record<string, UserResponse>
  );
  return { ...state, ...usersById };
});

// Загружаем данные пользователей для отображения имён в карточках репортов
sample({
  clock: loadReportsFx.doneData,
  fn: (reports) => {
    const ids = new Set<string>();

    reports.reports.forEach((report) => {
      if (report.responsibleUserId) ids.add(report.responsibleUserId);
      if (report.creatorUserId) ids.add(report.creatorUserId);

      report.participantsUserIds?.forEach((id) => {
        if (id) ids.add(id);
      });
    });

    return Array.from(ids);
  },
  filter: (data) => data.reports.length > 0,
  target: fetchReportsUsersFx,
});
