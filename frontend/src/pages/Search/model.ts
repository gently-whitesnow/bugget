import {
  createEffect,
  createEvent,
  createStore,
  sample,
  combine,
} from "effector";
import { searchReports } from "./api/searchReports";
import type { SearchRequestQueryParams, SearchResponse } from "./api/contracts";
import { UserResponse } from "@/shared/api";
import type { ReportStatuses } from "@/shared/config";
import { fetchUsers } from "@/entities/user";

/**
 * Команда в фильтре — выбор в UI, а не объект с провода: страница держит от неё
 * только идентификатор для запроса и имя для подписи. Типом контракта её
 * описывать нечем — подсказки отдают команду целиком, а в фильтр попадает
 * ровно эта пара.
 */
export type TeamFilter = { id: string; name: string };

export const searchFx = createEffect<SearchRequestQueryParams, SearchResponse>(
  async (params: SearchRequestQueryParams) => {
    // Пустые фильтры не уходят в URL — как и раньше; имена параметров теперь
    // берутся из контракта (`Search_SearchReports`), а не набираются строками.
    const result = await searchReports({
      query: params.query || undefined,
      sort: params.sort || undefined,
      userId: params.userId || undefined,
      teamId: params.teamId || undefined,
      skip: params.skip,
      take: params.take,
      reportStatuses: params.reportStatuses,
    });
    // `total` приходит каноном Int64String (строкой) — пустая выдача повторяет
    // ту же форму, а не подменяет её числом.
    return result || { reports: [], total: "0" };
  }
);

export const searchStarted = createEvent<SearchRequestQueryParams>();
export const searchPageOpened = createEvent();
export const searchPageClosed = createEvent();
export const loadMore = createEvent();

export const updateQuery = createEvent<string>();
export const updateSortField = createEvent<string>();
export const updateSortDirection = createEvent<"asc" | "desc">();
export const updateStatuses = createEvent<ReportStatuses[] | null>();
export const updateUserFilter = createEvent<string | null>();
export const updateTeamFilter = createEvent<TeamFilter | null>();

export const $query = createStore<string>("").on(updateQuery, (_, q) => q);
export const $sortField = createStore<string>("created").on(
  updateSortField,
  (_, field) => field
);
export const $sortDirection = createStore<"asc" | "desc">("desc").on(
  updateSortDirection,
  (_, direction) => direction
);
export const $statuses = createStore<ReportStatuses[] | null>(null).on(
  updateStatuses,
  (_, s) => s
);

export const $userFilter = createStore<string | null>(null).on(
  updateUserFilter,
  (_, userId) => userId
);

export const $teamFilter = createStore<TeamFilter | null>(null).on(
  updateTeamFilter,
  (_, team) => team
);

export const $skip = createStore<number>(0)
  .on(loadMore, (skip) => skip + 10)
  .reset([
    updateQuery,
    updateSortField,
    updateSortDirection,
    updateStatuses,
    updateUserFilter,
    updateTeamFilter,
  ]);

const itemsPerPage = 10;

export const $searchResult = createStore<SearchResponse>({
  reports: [],
  total: "0",
})
  .on(searchFx.doneData, (state, newData) => {
    // Проверяем, является ли это загрузкой дополнительных результатов
    // Если skip > 0, значит это loadMore
    if (newData.reports.length > 0 && state.reports.length > 0) {
      return {
        total: newData.total,
        reports: [...state.reports, ...newData.reports],
      };
    }
    return newData;
  })
  .reset([
    updateQuery,
    updateSortField,
    updateSortDirection,
    updateStatuses,
    updateUserFilter,
    updateTeamFilter,
  ]);

// Загрузка пользователей
export const fetchUsersFx = createEffect<string[], UserResponse[]>(
  async (userIds) => {
    if (userIds.length === 0) return [];
    return await fetchUsers(userIds);
  }
);

// стор для хранения пользователей по ID
export const $usersStore = createStore<Record<string, UserResponse>>({}).on(
  fetchUsersFx.doneData,
  (state, users) => {
    const usersById = users.reduce(
      (acc, user) => {
        acc[user.id] = user;
        return acc;
      },
      {} as Record<string, UserResponse>
    );
    return { ...state, ...usersById };
  }
);

// получаем все уникальные id пользователей для загрузки
export const $allUserIdsStore = combine(
  $searchResult,
  $userFilter,
  (searchResult, userFilter) => {
    const allIds = new Set<string>();

    // Добавляем ID пользователя из фильтра
    if (userFilter) {
      allIds.add(userFilter);
    }

    // Добавляем ID пользователей из результатов поиска
    searchResult.reports?.forEach((report) => {
      if (report.responsibleUserId) allIds.add(report.responsibleUserId);
      if (report.creatorUserId) allIds.add(report.creatorUserId);
      report.participantsUserIds?.forEach((id) => {
        if (id) allIds.add(id);
      });
      report.bugs?.forEach((bug) => {
        if (bug.creatorUserId) allIds.add(bug.creatorUserId);
        bug.comments?.forEach((comment) => {
          if (comment.creatorUserId) allIds.add(comment.creatorUserId);
        });
      });
    });
    return Array.from(allIds).filter(Boolean);
  }
);

// загрузка пользователей после обновления результатов поиска или фильтра
sample({
  clock: [$searchResult.updates, $userFilter.updates],
  source: $allUserIdsStore,
  filter: (userIds) => userIds.length > 0,
  target: fetchUsersFx,
});

sample({
  clock: searchStarted,
  target: searchFx,
});

// При изменении фильтров - запустить поиск
sample({
  source: {
    query: $query,
    sortField: $sortField,
    sortDirection: $sortDirection,
    reportStatuses: $statuses,
    userFilter: $userFilter,
    teamFilter: $teamFilter,
    skip: $skip,
  },
  clock: [
    $query.updates,
    $sortField.updates,
    $sortDirection.updates,
    $statuses.updates,
    updateUserFilter,
    $teamFilter.updates,
    $skip.updates,
  ],
  fn: ({
    query,
    sortField,
    sortDirection,
    reportStatuses,
    userFilter,
    teamFilter,
    skip,
  }) => ({
    query,
    sort: `${sortField}_${sortDirection}`,
    reportStatuses: reportStatuses ?? undefined,
    userId: userFilter ?? undefined,
    teamId: teamFilter?.id ?? undefined,
    skip,
    take: itemsPerPage,
  }),
  target: searchStarted,
});
