import { createEffect, createStore, sample } from "effector";
import { appApi, fetchUsers } from "@/shared/api";
import { ReportStatuses } from "@/shared/config";
import type { UserResponse } from "@/shared/api";

export type ReportListItem = {
  id: string;
  title: string;
  status: number;
  responsibleUserId: string;
  pastResponsibleUserId: string;
  creatorUserId: string;
  creatorTeamId?: string | null;
  createdAt: string;
  updatedAt: string;
  participantsUserIds: string[];
};

export type ListReportsResponse = {
  total: number;
  reports: ReportListItem[];
};

const defaultReportStatuses = [
  Number(ReportStatuses.BACKLOG),
  Number(ReportStatuses.FIX),
  Number(ReportStatuses.TEST),
];

type LoadReportsParams = {
  userId?: string | null;
  teamId?: string | null;
  statuses?: number[];
  creatorTypes?: number[];
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
    const searchParams = new URLSearchParams();
    if (userId) searchParams.append("userId", userId);
    if (teamId) searchParams.append("teamId", teamId);
    if (statuses) {
      for (const status of statuses) {
        searchParams.append("reportStatuses", String(status));
      }
    }
    if (creatorTypes) {
      for (const ct of creatorTypes) {
        searchParams.append("creatorTypes", String(ct));
      }
    }
    searchParams.append("skip", String(offset));
    if (take != null) searchParams.append("take", String(take));

    const { data } = await appApi.get<ListReportsResponse>(
      `/v2/reports?${searchParams.toString()}`
    );
    return data;
  }
);

// Эффект для загрузки пользователей репортов
export const fetchReportsUsersFx = createEffect<string[], UserResponse[]>(
  async (userIds) => {
    if (userIds.length === 0) return [];
    return await fetchUsers(userIds);
  }
);

// Стор для репортов
export const $reportsStore = createStore<ListReportsResponse>({
  total: 0,
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
