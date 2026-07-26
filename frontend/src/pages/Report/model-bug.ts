import { createEffect, createEvent, sample } from "effector";

import { createBug, updateBug } from "@/entities/report";
import type {
  CreateBugRequest,
  CreateBugResponse,
  PatchBugRequest,
  PatchBugResponse,
  BugFormData,
  BugUpdateData,
  BugClientEntity,
} from "@/entities/report";
import { BugStatuses } from "@/shared/config";
import { notificationMessages, notifyErrorRequested } from "@/shared/model";
import {
  createBugFxDoneDataEvent,
  updateBugFxDoneDataEvent,
} from "@/entities/report";

/**
 * Эффекты для API
 */
export const createBugFx = createEffect<
  { reportId: string; data: CreateBugRequest; clientId?: number },
  CreateBugResponse & { reportId: string; clientId: number }
>(async ({ reportId, data, clientId }) => {
  try {
    const result = await createBug(reportId, data);
    return { ...result, reportId, clientId: clientId ?? result.id };
  } catch (error) {
    console.error("❌ createBugFx error:", error);
    notifyErrorRequested({
      title: "Не удалось создать баг",
      message: notificationMessages.errorRetry,
      options: {
        dedupeKey: "report-bug-create-failed",
      },
    });
    throw error;
  }
});

export const updateBugFx = createEffect<
  { reportId: string; bugId: number; data: PatchBugRequest },
  PatchBugResponse
>(async ({ reportId, bugId, data }) => {
  try {
    const result = await updateBug(reportId, bugId, data);
    return result;
  } catch (error) {
    console.error("❌ updateBugFx error:", error);
    const hasReceive = "receive" in data;
    const hasExpect = "expect" in data;
    notifyErrorRequested({
      title:
        hasReceive && hasExpect
          ? "Не удалось сохранить фактический и ожидаемый результаты"
          : hasReceive
            ? "Не удалось сохранить фактический результат"
            : hasExpect
              ? "Не удалось сохранить ожидаемый результат"
              : "Не удалось обновить баг",
      message: notificationMessages.errorRetry,
      options: {
        dedupeKey:
          hasReceive && hasExpect
            ? "report-bug-both-results-update-failed"
            : hasReceive
              ? "report-bug-receive-update-failed"
              : hasExpect
                ? "report-bug-expect-update-failed"
                : "report-bug-update-failed",
      },
    });
    throw error;
  }
});

/**
 * События
 */
export const createBugEvent = createEvent<{
  reportId: string;
  data: BugFormData;
}>();

export const updateBugDataEvent = createEvent<BugUpdateData>();

export const changeBugStatusEvent = createEvent<{
  bugId: number;
  status: BugStatuses;
}>();

/**
 * Связи - обновление сторов в entities через события
 */
sample({
  clock: createBugFx.doneData,
  fn: (result) =>
    ({
      ...result,
      attachments: null,
      comments: null,
      isLocalOnly: false,
    }) as BugClientEntity & { reportId: string; clientId: number },
  target: createBugFxDoneDataEvent,
});

sample({
  clock: updateBugFx.doneData,
  fn: (updatedBug: PatchBugResponse) => ({
    id: updatedBug.id,
    title: updatedBug.title,
    receive: updatedBug.receive,
    expect: updatedBug.expect,
    status: updatedBug.status,
    updatedAt: updatedBug.updatedAt,
  }),
  target: updateBugFxDoneDataEvent,
});

/**
 * Сэмплы для обработки пользовательских действий
 */
sample({
  clock: createBugEvent,
  fn: ({ reportId, data }) => ({
    reportId,
    data,
    clientId: Date.now(),
  }),
  target: createBugFx,
});

sample({
  clock: updateBugDataEvent,
  fn: ({ bugId, reportId, data }) => ({
    reportId,
    bugId,
    data,
  }),
  target: updateBugFx,
});
