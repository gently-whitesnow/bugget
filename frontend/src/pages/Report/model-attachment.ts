import {
  createEffect,
  createEvent,
  createStore,
  sample,
  combine,
} from "effector";

import {
  deleteBugAttachment,
  renameBugAttachment,
  uploadAttachment,
} from "@/entities/report";
import type { AttachmentResponse } from "@/entities/report";
import { Attachment } from "@/entities/report";
import { notificationMessages, notifyErrorRequested } from "@/shared/model";

import { setBugsEvent } from "@/entities/report";

/**
 * Сторы
 */

// Стор для всех сущностей attachment c ключём по ID
export const $attachmentsStore = createStore<Record<number, Attachment>>({});

// Стор для маппинга id багов с attachment id
export const $bugAttachmentsStore = createStore<Record<number, number[]>>({});

/**
 * Эффекты
 */

export const uploadAttachmentFx = createEffect<
  { reportId: string; bugId: number; attachType: number; file: File },
  { attachment: AttachmentResponse; bugId: number }
>(async ({ reportId, bugId, attachType, file }) => {
  try {
    const result = await uploadAttachment({
      reportId,
      bugId,
      attachType,
      file,
    });
    return { attachment: result, bugId };
  } catch (error) {
    console.error("❌ uploadAttachmentFx error:", error);
    notifyErrorRequested({
      title: "Не удалось загрузить файл",
      message: notificationMessages.errorRetry,
      options: {
        dedupeKey: "report-bug-attachment-upload-failed",
      },
    });
    throw error;
  }
});

export const deleteAttachmentFx = createEffect<
  { reportId: string; bugId: number; attachmentId: number },
  { bugId: number; attachmentId: number }
>(async ({ reportId, bugId, attachmentId }) => {
  try {
    await deleteBugAttachment(reportId, bugId, attachmentId);
    return { bugId, attachmentId };
  } catch (error) {
    console.error("❌ deleteAttachmentFx error:", error);
    notifyErrorRequested({
      title: "Не удалось удалить файл",
      message: notificationMessages.errorRetry,
      options: {
        dedupeKey: "report-bug-attachment-delete-failed",
      },
    });
    throw error;
  }
});

export const renameAttachmentFx = createEffect<
  {
    reportId: string;
    bugId: number;
    attachmentId: number;
    fileName: string;
  },
  { attachment: AttachmentResponse; bugId: number }
>(async ({ reportId, bugId, attachmentId, fileName }) => {
  try {
    const attachment = await renameBugAttachment({
      reportId,
      bugId,
      attachmentId,
      fileName,
    });
    return { attachment, bugId };
  } catch (error) {
    console.error("❌ renameAttachmentFx error:", error);
    notifyErrorRequested({
      title: "Не удалось переименовать файл",
      message: notificationMessages.errorRetry,
      options: {
        dedupeKey: "report-bug-attachment-rename-failed",
      },
    });
    throw error;
  }
});

/**
 * События
 */

export const uploadAttachmentEvent = createEvent<{
  reportId: string;
  bugId: number;
  attachType: number;
  file: File;
}>();

export const deleteAttachmentEvent = createEvent<{
  reportId: string;
  bugId: number;
  attachmentId: number;
}>();

// socket события
export const bugAttachmentCreatedSocketEvent = createEvent<Attachment>();
export const bugAttachmentChangedSocketEvent = createEvent<Attachment>();
export const bugAttachmentDeletedSocketEvent = createEvent<{
  bugId: number;
  attachmentId: number;
}>();

/**
 * Логика
 */

// заполняем attachments при загрузке багов из основного репорта
$attachmentsStore.on(setBugsEvent, (state, { bugs }) => {
  const attachmentsById: Record<number, Attachment> = {};
  bugs.forEach((bug) => {
    if (bug.attachments) {
      bug.attachments.forEach((att) => {
        attachmentsById[att.id] = att;
      });
    }
  });
  return { ...state, ...attachmentsById };
});

$bugAttachmentsStore.on(setBugsEvent, (state, { bugs }) => {
  const bugAttachments: Record<number, number[]> = {};
  bugs.forEach((bug) => {
    // не перезаписываем существующие attachments для багов не в payload
    if (bug.attachments) {
      bugAttachments[bug.id] = bug.attachments.map((att) => att.id);
    }
  });
  return { ...state, ...bugAttachments };
});

$attachmentsStore.on(uploadAttachmentFx.doneData, (state, { attachment }) => {
  return {
    ...state,
    [attachment.id]: attachment,
  };
});

$attachmentsStore
  .on(bugAttachmentCreatedSocketEvent, (state, attachment) => ({
    ...state,
    [attachment.id]: attachment,
  }))
  .on(bugAttachmentChangedSocketEvent, (state, attachment) => {
    const current = state[attachment.id];
    if (!current) return state;

    return {
      ...state,
      [attachment.id]: {
        ...current,
        ...attachment,
      },
    };
  })
  .on(bugAttachmentDeletedSocketEvent, (state, { attachmentId }) => {
    const newState = { ...state };
    delete newState[attachmentId];
    return newState;
  });

$bugAttachmentsStore.on(
  uploadAttachmentFx.doneData,
  (state, { attachment, bugId }) => {
    const currentAttachments = state[bugId] || [];
    return {
      ...state,
      [bugId]: [...currentAttachments, attachment.id],
    };
  }
);

$bugAttachmentsStore.on(
  bugAttachmentCreatedSocketEvent,
  (state, attachment) => {
    const currentAttachments = state[attachment.entityId] || [];
    if (currentAttachments.includes(attachment.id)) return state;

    return {
      ...state,
      [attachment.entityId]: [...currentAttachments, attachment.id],
    };
  }
);

$attachmentsStore.on(deleteAttachmentFx.doneData, (state, { attachmentId }) => {
  const newState = { ...state };
  delete newState[attachmentId];
  return newState;
});

$attachmentsStore.on(renameAttachmentFx.doneData, (state, { attachment }) => ({
  ...state,
  [attachment.id]: attachment,
}));

$bugAttachmentsStore.on(
  deleteAttachmentFx.doneData,
  (state, { bugId, attachmentId }) => {
    const currentAttachments = state[bugId] || [];
    return {
      ...state,
      [bugId]: currentAttachments.filter((id) => id !== attachmentId),
    };
  }
);

$bugAttachmentsStore.on(
  bugAttachmentDeletedSocketEvent,
  (state, { bugId, attachmentId }) => {
    const currentAttachments = state[bugId] || [];
    if (!currentAttachments.includes(attachmentId)) return state;

    return {
      ...state,
      [bugId]: currentAttachments.filter((id) => id !== attachmentId),
    };
  }
);

/**
 * Сэмплы
 */

sample({
  clock: uploadAttachmentEvent,
  target: uploadAttachmentFx,
});

sample({
  clock: deleteAttachmentEvent,
  target: deleteAttachmentFx,
});

/**
 * Combined-сторы
 */

export const $attachmentsData = combine(
  $attachmentsStore,
  $bugAttachmentsStore,
  (attachments, bugAttachments) => ({ attachments, bugAttachments })
);
