import { createEffect, createEvent, createStore, sample } from "effector";
import {
  createComment,
  updateComment,
  deleteComment,
  createCommentAttachment,
  deleteCommentAttachment,
  renameCommentAttachment,
} from "@/entities/report";
import {
  attachmentFromSocket,
  commentFromSocket,
  commentUpdateFromSocket,
} from "@/entities/report";
import type {
  Comment,
  CommentSummaryResponse,
  Attachment,
} from "@/entities/report";
import { AttachmentTypes, type CommentAudiences } from "@/shared/config";
import { notificationMessages, notifyErrorRequested } from "@/shared/model";
import type {
  AttachmentSocketResponse,
  CommentSocketResponse,
} from "@/shared/model";

/**
 * Сторы
 *
 * Комментарий в сторе — форма контракта (`Comment`): с ключом `attachments`,
 * где `null` значит «вложения не приезжали с этим ответом». Ручки создания и
 * обновления, как и события SignalR, отдают `CommentSummary` — без вложений;
 * такой комментарий кладётся в стор с `attachments: null`, а при обновлении
 * существующего его вложения сохраняются.
 */
type CommentsByBugId = Record<number, Comment[]>;

export const $commentsByBugId = createStore<CommentsByBugId>({});

/**
 * Обновляет комментарий по его id, не зная бага: события SignalR о вложениях
 * приносят только `entityId` комментария. Если комментария в сторе нет или
 * обновление ничего не меняет (`patch` вернул `null`), стор остаётся прежним.
 */
const patchCommentById = (
  state: CommentsByBugId,
  commentId: number,
  patch: (comment: Comment) => Comment | null
): CommentsByBugId => {
  for (const [bugId, comments] of Object.entries(state)) {
    const idx = comments.findIndex((comment) => comment.id === commentId);
    if (idx === -1) continue;

    const updated = patch(comments[idx]);
    if (!updated) return state;

    return {
      ...state,
      [Number(bugId)]: [
        ...comments.slice(0, idx),
        updated,
        ...comments.slice(idx + 1),
      ],
    };
  }

  return state;
};

/**
 * Эффекты
 */

export const createCommentFx = createEffect<
  {
    reportId: string;
    bugId: number;
    text: string;
    audience?: CommentAudiences;
  },
  Comment,
  Error
>(async ({ reportId, bugId, text, audience }) => {
  try {
    const result = await createComment(reportId, bugId, { text, audience });
    return {
      id: result.id,
      bugId: result.bugId,
      text: result.text,
      creatorUserId: result.creatorUserId,
      createdAt: result.createdAt,
      updatedAt: result.updatedAt,
      creatorType: result.creatorType,
      audience: result.audience,
      // Ручка создания вложений не отдаёт: у нового комментария их ещё нет.
      attachments: null,
    };
  } catch (error) {
    console.error("❌ createCommentFx error:", error);
    notifyErrorRequested({
      title: "Не удалось добавить комментарий",
      message: notificationMessages.errorRetry,
      options: {
        dedupeKey: "report-comment-create-failed",
      },
    });
    throw error;
  }
});

export const updateCommentFx = createEffect<
  {
    reportId: string;
    bugId: number;
    commentId: number;
    text: string;
  },
  // Ответ обновления — `CommentSummary`: вложения он не отдаёт, и стор их не
  // теряет, потому что обновление накладывается на существующий комментарий.
  CommentSummaryResponse,
  Error
>(async ({ reportId, bugId, commentId, text }) => {
  try {
    const result = await updateComment(reportId, bugId, commentId, { text });
    return {
      id: result.id,
      bugId: result.bugId,
      text: result.text,
      creatorUserId: result.creatorUserId,
      creatorType: result.creatorType,
      audience: result.audience,
      createdAt: result.createdAt,
      updatedAt: result.updatedAt,
    };
  } catch (error) {
    console.error("❌ updateCommentFx error:", error);
    notifyErrorRequested({
      title: "Не удалось обновить комментарий",
      message: notificationMessages.errorRetry,
      options: {
        dedupeKey: "report-comment-update-failed",
      },
    });
    throw error;
  }
});

export const deleteCommentFx = createEffect<
  {
    reportId: string;
    bugId: number;
    commentId: number;
  },
  void,
  Error
>(async ({ reportId, bugId, commentId }) => {
  try {
    await deleteComment(reportId, bugId, commentId);
  } catch (error) {
    console.error("deleteCommentFx error:", error);
    notifyErrorRequested({
      title: "Не удалось удалить комментарий",
      message: notificationMessages.errorRetry,
      options: {
        dedupeKey: "report-comment-delete-failed",
      },
    });
    throw error;
  }
});

export const createCommentAttachmentFx = createEffect<
  { reportId: string; bugId: number; commentId: number; file: File },
  { bugId: number; commentId: number; attachment: Attachment }
>(async ({ reportId, bugId, commentId, file }) => {
  try {
    const attachment = await createCommentAttachment(
      reportId,
      bugId,
      commentId,
      file
    );
    return { bugId, commentId, attachment };
  } catch (error) {
    notifyErrorRequested({
      title: "Не удалось загрузить вложение",
      message: notificationMessages.errorRetry,
      options: {
        dedupeKey: "report-comment-attachment-upload-failed",
      },
    });
    throw error;
  }
});

export const deleteCommentAttachmentFx = createEffect<
  { reportId: string; bugId: number; commentId: number; attachmentId: number },
  { bugId: number; commentId: number; attachmentId: number }
>(async ({ reportId, bugId, commentId, attachmentId }) => {
  try {
    await deleteCommentAttachment(reportId, bugId, commentId, attachmentId);
    return { bugId, commentId, attachmentId };
  } catch (error) {
    notifyErrorRequested({
      title: "Не удалось удалить вложение",
      message: notificationMessages.errorRetry,
      options: {
        dedupeKey: "report-comment-attachment-delete-failed",
      },
    });
    throw error;
  }
});

export const renameCommentAttachmentFx = createEffect<
  {
    reportId: string;
    bugId: number;
    commentId: number;
    attachmentId: number;
    fileName: string;
  },
  { bugId: number; commentId: number; attachment: Attachment }
>(async ({ reportId, bugId, commentId, attachmentId, fileName }) => {
  try {
    const attachment = await renameCommentAttachment({
      reportId,
      bugId,
      commentId,
      attachmentId,
      fileName,
    });
    return { bugId, commentId, attachment };
  } catch (error) {
    notifyErrorRequested({
      title: "Не удалось переименовать вложение",
      message: notificationMessages.errorRetry,
      options: {
        dedupeKey: "report-comment-attachment-rename-failed",
      },
    });
    throw error;
  }
});

/**
 * События
 */

export const createCommentEvent = createEvent<{
  reportId: string;
  bugId: number;
  text: string;
}>();

export const updateCommentEvent = createEvent<{
  reportId: string;
  bugId: number;
  commentId: number;
  text: string;
}>();

export const deleteCommentEvent = createEvent<{
  reportId: string;
  bugId: number;
  commentId: number;
}>();

export const setCommentsByBugIdEvent = createEvent<
  {
    bugId: number;
    comments: Comment[];
  }[]
>();

// socket события: payload описан типами realtime-контракта, а не HTTP-схемами;
// в сущности стора его переводят адаптеры `*FromSocket` (ADR-0007).
export const createCommentSocketEvent = createEvent<CommentSocketResponse>();
export const updateCommentSocketEvent = createEvent<CommentSocketResponse>();
export const deleteCommentSocketEvent = createEvent<{
  bugId: number;
  commentId: number;
}>();

export const createCommentAttachmentSocketEvent =
  createEvent<AttachmentSocketResponse>();
export const commentAttachmentChangedSocketEvent =
  createEvent<AttachmentSocketResponse>();
export const deleteCommentAttachmentSocketEvent = createEvent<{
  commentId: number;
  attachmentId: number;
}>();

/**
 * Логика
 */

$commentsByBugId
  .on(setCommentsByBugIdEvent, (state, allComments) => {
    const newState = { ...state };
    allComments.forEach(({ bugId, comments }) => {
      newState[bugId] = comments;
    });
    return newState;
  })
  .on(createCommentSocketEvent, (state, payload) => {
    const existingComments = state[payload.bugId] || [];
    if (existingComments.some((c) => c.id === payload.id)) return state;

    return {
      ...state,
      [payload.bugId]: [...existingComments, commentFromSocket(payload)],
    };
  })
  .on(updateCommentSocketEvent, (state, payload) => {
    const existingComments = state[payload.bugId] || [];
    if (!existingComments.length) return state;

    return {
      ...state,
      [payload.bugId]: existingComments.map((c) =>
        c.id === payload.id ? commentUpdateFromSocket(c, payload) : c
      ),
    };
  })
  .on(createCommentFx.doneData, (state, comment) => {
    const bugId = comment.bugId;
    const existingComments = state[bugId] || [];
    if (existingComments.some((c) => c.id === comment.id)) return state;
    return { ...state, [bugId]: [...existingComments, comment] };
  })
  .on(updateCommentFx.doneData, (state, updatedComment) => {
    const bugId = updatedComment.bugId;
    const existingComments = state[bugId] || [];
    return {
      ...state,
      [bugId]: existingComments.map((c) =>
        c.id === updatedComment.id ? { ...c, ...updatedComment } : c
      ),
    };
  })
  .on(deleteCommentEvent, (state, { bugId, commentId }) => {
    const existingComments = state[bugId] || [];
    return {
      ...state,
      [bugId]: existingComments.filter((c) => c.id !== commentId),
    };
  })
  .on(deleteCommentSocketEvent, (state, { bugId, commentId }) => {
    const existingComments = state[bugId] || [];
    if (!existingComments.length) return state;

    return {
      ...state,
      [bugId]: existingComments.filter((c) => c.id !== commentId),
    };
  })
  .on(
    createCommentAttachmentFx.doneData,
    (state, { bugId, commentId, attachment }) => {
      const existingComments = state[bugId] || [];
      return {
        ...state,
        [bugId]: existingComments.map((comment) => {
          if (comment.id !== commentId) return comment;
          const currentAttachments = comment.attachments || [];
          if (currentAttachments.some((a) => a.id === attachment.id))
            return comment;
          return {
            ...comment,
            attachments: [...currentAttachments, attachment],
          };
        }),
      };
    }
  )
  .on(createCommentAttachmentSocketEvent, (state, payload) => {
    const attachment = attachmentFromSocket(payload);

    return patchCommentById(state, payload.entityId, (comment) => {
      const currentAttachments = comment.attachments || [];
      if (currentAttachments.some((a) => a.id === attachment.id)) return null;

      return { ...comment, attachments: [...currentAttachments, attachment] };
    });
  })
  .on(
    deleteCommentAttachmentFx.doneData,
    (state, { bugId, commentId, attachmentId }) => {
      const existingComments = state[bugId] || [];
      return {
        ...state,
        [bugId]: existingComments.map((comment) =>
          comment.id === commentId
            ? {
                ...comment,
                attachments: (comment.attachments || []).filter(
                  (a) => a.id !== attachmentId
                ),
              }
            : comment
        ),
      };
    }
  )
  .on(
    renameCommentAttachmentFx.doneData,
    (state, { bugId, commentId, attachment }) => {
      const existingComments = state[bugId] || [];
      return {
        ...state,
        [bugId]: existingComments.map((comment) =>
          comment.id === commentId
            ? {
                ...comment,
                attachments: (comment.attachments || []).map((item) =>
                  item.id === attachment.id ? { ...item, ...attachment } : item
                ),
              }
            : comment
        ),
      };
    }
  )
  .on(
    deleteCommentAttachmentSocketEvent,
    (state, { commentId, attachmentId }) =>
      patchCommentById(state, commentId, (comment) => ({
        ...comment,
        attachments: (comment.attachments || []).filter(
          (a) => a.id !== attachmentId
        ),
      }))
  )
  .on(commentAttachmentChangedSocketEvent, (state, payload) => {
    const attachment = attachmentFromSocket(payload);

    if (attachment.attachType !== AttachmentTypes.COMMENT) return state;

    return patchCommentById(state, payload.entityId, (comment) => ({
      ...comment,
      attachments: (comment.attachments || []).map((a) =>
        a.id === attachment.id ? attachment : a
      ),
    }));
  });

/**
 * Сэмплы
 */

sample({
  clock: createCommentEvent,
  target: createCommentFx,
});

sample({
  clock: updateCommentEvent,
  target: updateCommentFx,
});

sample({
  clock: deleteCommentEvent,
  target: deleteCommentFx,
});
