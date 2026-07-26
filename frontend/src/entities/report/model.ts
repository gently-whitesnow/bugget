import { createReport, fetchReport, patchReport } from "./api";
import type {
  CreateReportResponse,
  PatchReportRequest,
  PatchReportResponse,
  ReportResponse,
} from "./api";
import { fetchUsers } from "@/shared/api";
import type { UserResponse } from "@/shared/api/contracts";

import { ReportStatuses, BugStatuses, CreatorTypes } from "@/shared/config";
import {
  createEffect,
  createEvent,
  createStore,
  sample,
  combine,
} from "effector";
import type {
  CreateBugSocketResponse,
  PatchBugSocketResponse,
  PatchReportSocketResponse,
} from "@/shared/model";
import { notificationMessages, notifyErrorRequested } from "@/shared/model";
import type { BugClientEntity } from "./model/types";

/**
 * Эффекты
 */
export const getReportFx = createEffect<string, ReportResponse>(async (id) => {
  return await fetchReport(id);
});

export const createReportFx = createEffect<string, CreateReportResponse>(
  async (title) => {
    try {
      return await createReport({ title });
    } catch (error) {
      notifyErrorRequested({
        title: "Не удалось создать репорт",
        message: notificationMessages.errorRetry,
        options: {
          dedupeKey: "report-create-failed",
        },
      });
      throw error;
    }
  }
);

export const patchReportFx = createEffect<
  { id: string; patchRequest: PatchReportRequest },
  PatchReportResponse
>(async ({ id, patchRequest }) => {
  try {
    return await patchReport(id, patchRequest);
  } catch (error) {
    notifyErrorRequested({
      title: "Не удалось обновить репорт",
      message: notificationMessages.errorRetry,
      options: {
        dedupeKey: "report-patch-failed",
      },
    });
    throw error;
  }
});

export const fetchUsersFx = createEffect<string[], UserResponse[]>(
  async (userIds) => {
    if (userIds.length === 0) return [];
    return await fetchUsers(userIds);
  }
);

/**
 * События
 */
export const changeTitleEvent = createEvent<string>();
export const saveTitleEvent = createEvent<void>();
export const changeStatusEvent = createEvent<ReportStatuses>();
export const changeResponsibleUserIdEvent = createEvent<string | null>();
export const patchReportSocketEvent = createEvent<PatchReportSocketResponse>();
export const addParticipantSocketEvent = createEvent<string>();
export const updateResponsibleUserIdEvent = createEvent<string>();
export const updateCreatorUserIdEvent = createEvent<string>();
export const updateReportPathIdEvent = createEvent<string | null>();
export const updateIsExcludedFromAnalyticsEvent = createEvent<boolean>();
export const clearReport = createEvent<void>();

export const setBugsEvent = createEvent<{
  reportId: string;
  bugs: BugClientEntity[];
}>();
export const createBugFxDoneDataEvent = createEvent<
  BugClientEntity & { reportId: string; clientId: number }
>();
export const createBugSocketEvent = createEvent<{
  reportId: string;
  bug: CreateBugSocketResponse;
}>();
export const updateBugFxDoneDataEvent = createEvent<{
  id: number;
  title: string | null;
  receive: string | null;
  expect: string | null;
  status: number;
  updatedAt: string;
}>();
export const patchBugSocketEvent = createEvent<{
  bugId: number;
  patch: PatchBugSocketResponse;
}>();
export const clearBugsEvent = createEvent<void>();

/**
 * Сторы
 */
export const $reportPathStore = createStore<string | null>(null)
  .on(updateReportPathIdEvent, (_, reportPath) => reportPath)
  .reset(clearReport);

// источник данных для других сторов
export const $initialReportStore = createStore<ReportResponse | null>(null)
  .on(getReportFx.doneData, (_, report) => report)
  .on(createReportFx.doneData, (state, report) => ({
    ...state,
    id: report.id,
    title: report.title,
    status: report.status,
    responsibleUserId: report.responsibleUserId,
    pastResponsibleUserId: report.responsibleUserId,
    creatorUserId: report.creatorUserId,
    creatorType: report.creatorType,
    createdAt: report.createdAt,
    updatedAt: report.updatedAt,
    participantsUserIds: [],
    links: [],
    bugs: [],
  }))
  .reset(clearReport);

export const $titleStore = createStore<string>("")
  .on(getReportFx.doneData, (_, report) => report.title)
  .on(patchReportSocketEvent, (state, report) => report.title ?? state)
  .on(changeTitleEvent, (_, title) => title)
  .reset(clearReport);

export const $statusStore = createStore<ReportStatuses>(ReportStatuses.BACKLOG)
  .on(getReportFx.doneData, (_, report) => report.status)
  .on(patchReportFx.doneData, (state, report) => report.status ?? state)
  .on(patchReportSocketEvent, (state, report) => report.status ?? state)
  .reset(clearReport);

export const $responsibleUserIdStore = createStore<string>("")
  .on(getReportFx.doneData, (_, report) => report.responsibleUserId)
  .on(
    patchReportSocketEvent,
    (state, report) => report.responsibleUserId ?? state
  )
  .reset(clearReport);

export const $creatorUserIdStore = createStore<string>("")
  .on(getReportFx.doneData, (_, report) => report.creatorUserId)
  .reset(clearReport);

export const $creatorTypeStore = createStore<number>(CreatorTypes.USER)
  .on(getReportFx.doneData, (_, report) => report.creatorType)
  .on(createReportFx.doneData, (_, report) => report.creatorType)
  .reset(clearReport);

export const $pastResponsibleUserIdStore = createStore<string>("")
  .on(getReportFx.doneData, (_, report) => report.pastResponsibleUserId)
  .on(
    patchReportFx.doneData,
    (state, report) => report.pastResponsibleUserId ?? state
  )
  .on(
    patchReportSocketEvent,
    (state, report) => report.pastResponsibleUserId ?? state
  )
  .reset(clearReport);

export const $updatedAtStore = createStore<string>(new Date().toISOString())
  .on(getReportFx.doneData, (_, report) => report.updatedAt)
  .on(createReportFx.doneData, (_, report) => report.updatedAt)
  .on(patchReportFx.doneData, (_, report) => report.updatedAt)
  .on(patchReportSocketEvent, (_, report) => {
    console.log("🔄 [Report] Updated at:", report.updatedAt);
    return report.updatedAt;
  })
  .reset(clearReport);

export const $reportIdStore = createStore<string | null>(null)
  .on($initialReportStore, (_, report) => report?.id ?? null)
  .reset(clearReport);

export const $isExcludedFromAnalyticsStore = createStore<boolean>(false)
  .on(
    getReportFx.doneData,
    (_, report) => report.isExcludedFromAnalytics ?? false
  )
  .on(updateIsExcludedFromAnalyticsEvent, (_, value) => value)
  .reset(clearReport);

export const $participantsUserIdsStore = createStore<string[]>([])
  .on(getReportFx.doneData, (_, report) => report.participantsUserIds)
  .on(addParticipantSocketEvent, (state, newParticipant) => {
    if (state.includes(newParticipant)) return state;
    return [...state, newParticipant];
  })
  .reset(clearReport);

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

// получаем имя ответственного пользователя
export const $responsibleUserNameStore = combine(
  $responsibleUserIdStore,
  $usersStore,
  (responsibleUserId, users) => {
    if (!responsibleUserId) return "";
    return users[responsibleUserId]?.name || "";
  }
);

// получаем имя последнего ответственного пользователя
export const $lastResponsibleUserNameStore = combine(
  $pastResponsibleUserIdStore,
  $usersStore,
  (pastResponsibleUserId, users) => {
    if (!pastResponsibleUserId) return "";
    return users[pastResponsibleUserId]?.name || "";
  }
);

// получаем участников с именами
export const $participantsWithNamesStore = combine(
  $participantsUserIdsStore,
  $usersStore,
  (participantsIds, users) => {
    return participantsIds
      .map((id) => users[id])
      .filter(Boolean)
      .map((user) => ({
        id: user.id,
        name: user.name,
        imageUrl: user.imageUrl ?? null,
      }));
  }
);

// получаем все уникальные id пользователей для загрузки
export const $allUserIdsStore = combine(
  $responsibleUserIdStore,
  $creatorUserIdStore,
  $participantsUserIdsStore,
  $pastResponsibleUserIdStore,
  (
    responsibleUserId,
    creatorUserId,
    participantsIds,
    pastResponsibleUserId
  ) => {
    const allIds = [
      responsibleUserId,
      creatorUserId,
      ...participantsIds,
      pastResponsibleUserId,
    ].filter(Boolean);
    return [...new Set(allIds)];
  }
);

// Стор для всех багов по id
export const $bugsStore = createStore<Record<number, BugClientEntity>>({})
  .on(setBugsEvent, (state, { bugs }) => {
    const bugsById = bugs.reduce(
      (acc, bug) => {
        acc[bug.id] = bug;
        return acc;
      },
      {} as Record<number, BugClientEntity>
    );
    return { ...state, ...bugsById };
  })
  .on(createBugFxDoneDataEvent, (state, newBug) => ({
    ...state,
    [newBug.id]: {
      ...newBug,
      reportId: newBug.reportId,
      attachments: null,
      comments: null,
      clientId: newBug.clientId,
    } as BugClientEntity,
  }))
  .on(createBugSocketEvent, (state, { bug, reportId }) => {
    if (state[bug.id]) return state;

    return {
      ...state,
      [bug.id]: {
        id: bug.id,
        reportId,
        title: bug.title,
        receive: bug.receive,
        expect: bug.expect,
        creatorUserId: bug.creatorUserId,
        createdAt: bug.createdAt,
        updatedAt: bug.updatedAt,
        status: bug.status,
        attachments: null,
        comments: null,
        clientId: bug.id,
        isLocalOnly: false,
      },
    };
  })
  .on(updateBugFxDoneDataEvent, (state, updatedBug) => {
    const existingBug = state[updatedBug.id];
    if (!existingBug) return state;

    return {
      ...state,
      [updatedBug.id]: {
        ...existingBug,
        title: updatedBug.title,
        receive: updatedBug.receive,
        expect: updatedBug.expect,
        status: updatedBug.status as BugStatuses,
        updatedAt: updatedBug.updatedAt,
      },
    };
  })
  .on(patchBugSocketEvent, (state, { bugId, patch }) => {
    const existingBug = state[bugId];
    if (!existingBug) return state;

    return {
      ...state,
      [bugId]: {
        ...existingBug,
        title: patch.title ?? existingBug.title,
        receive: patch.receive ?? existingBug.receive,
        expect: patch.expect ?? existingBug.expect,
        status: patch.status ?? existingBug.status,
      },
    };
  })
  .reset(clearBugsEvent);

// Список id багов для каждого репорта
export const $reportBugIdsStore = createStore<Record<string, number[]>>({})
  .on(setBugsEvent, (state, { reportId, bugs }) => ({
    ...state,
    [reportId]: bugs.map((bug) => bug.id),
  }))
  .on(createBugFxDoneDataEvent, (state, newBug) => {
    const bugIds = state[newBug.reportId] || [];
    return {
      ...state,
      [newBug.reportId]: [...bugIds, newBug.id],
    };
  })
  .on(createBugSocketEvent, (state, { reportId, bug }) => {
    const bugIds = state[reportId] || [];
    if (bugIds.includes(bug.id)) return state;

    return {
      ...state,
      [reportId]: [...bugIds, bug.id],
    };
  })
  .reset(clearBugsEvent);

// Combined store из всех багов и id багов для каждого репорта
export const $bugsData = combine(
  $bugsStore,
  $reportBugIdsStore,
  (bugs, reportBugIds) => ({ bugs, reportBugIds })
);

// Стор для багов текущего репорта
export const $reportBugsStore = combine(
  $bugsData,
  $reportIdStore,
  (bugsData, reportId) => {
    if (!reportId) return [];

    const { bugs, reportBugIds } = bugsData;
    const bugIds = reportBugIds[reportId] || [];

    return bugIds
      .map((id: number) => bugs[id])
      .filter(Boolean)
      .map((bug) => ({
        ...bug,
        clientId: bug.clientId || bug.id,
      }));
  }
);

// Фильтр багов по статусу
export const setBugStatusFilterEvent = createEvent<BugStatuses | null>();
export const $bugStatusFilterStore = createStore<BugStatuses | null>(null).on(
  setBugStatusFilterEvent,
  (current, next) => (current === next ? null : next)
);

/**
 * Связи
 */
// загрузка открытого репорта
sample({
  clock: updateReportPathIdEvent,
  filter: (pathId) => pathId !== null,
  target: getReportFx,
});

// загрузка пользователей после загрузки репорта
sample({
  clock: getReportFx.doneData,
  source: $allUserIdsStore,
  filter: (userIds) => userIds.length > 0,
  target: fetchUsersFx,
});

// загрузка пользователей при изменении списка участников через сокет
sample({
  clock: patchReportSocketEvent,
  source: $allUserIdsStore,
  filter: (userIds) => userIds.length > 0,
  target: fetchUsersFx,
});

// загрузка нового участника после получения его через сокет
sample({
  source: $usersStore,
  clock: addParticipantSocketEvent,
  filter: (users, newId) => !users[newId],
  fn: (_, newId) => [newId],
  target: fetchUsersFx,
});

// создание репорта
sample({
  clock: saveTitleEvent,
  source: {
    id: $reportIdStore,
    title: $titleStore,
  },
  filter: ({ id }) => id === null,
  fn: ({ title }) => title.trim(),
  target: createReportFx,
});

// изменение названия репорта
sample({
  clock: saveTitleEvent,
  source: {
    id: $reportIdStore,
    title: $titleStore,
  },
  filter: ({ id }) =>
    id !== null &&
    $initialReportStore.getState()?.title !== $titleStore.getState(),
  fn: ({ id, title }) => ({
    id: id!,
    patchRequest: { title },
  }),
  target: patchReportFx,
});

sample({
  clock: changeStatusEvent,
  source: $reportIdStore,
  filter: (id): id is string => id !== null,
  fn: (id, status) => ({
    id: id!,
    patchRequest: { status },
  }),
  target: patchReportFx,
});

sample({
  clock: changeStatusEvent,
  target: $statusStore,
});

sample({
  clock: changeResponsibleUserIdEvent,
  source: $reportIdStore,
  filter: (id): id is string => id !== null,
  fn: (id, responsibleUserId) => ({
    id: id!,
    patchRequest: { responsibleUserId },
  }),
  target: patchReportFx,
});

sample({
  clock: changeResponsibleUserIdEvent,
  fn: (responsibleUserId) => responsibleUserId ?? "",
  target: $responsibleUserIdStore,
});
