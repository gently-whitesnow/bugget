import type {
  AttachmentSocketResponse,
  BugStepSocketResponse,
  CommentSocketResponse,
  CreateBugSocketResponse,
  ReportLinkSocketResponse,
} from "@/shared/model";
import type {
  Attachment,
  BugClientEntity,
  BugStep,
  Comment,
  ReportLink,
} from "../model/types";
import {
  attachTypeFromSocket,
  bugStatusFromSocket,
  commentAudienceFromSocket,
  creatorTypeFromSocket,
} from "./socketEnums";

// Шов realtime → стор. SignalR (`events.yaml`) не генерируется и не типизируется
// HTTP-схемой (ADR-0007): иначе правка OpenAPI молча меняла бы realtime-путь.
// Поля перечислены по именам намеренно: пропавшее поле — ошибка компиляции.

export const attachmentFromSocket = (
  payload: AttachmentSocketResponse
): Attachment => ({
  id: payload.id,
  entityId: payload.entityId,
  attachType: attachTypeFromSocket(payload.attachType),
  createdAt: payload.createdAt,
  creatorUserId: payload.creatorUserId,
  fileName: payload.fileName,
  hasPreview: payload.hasPreview,
});

export const bugStepFromSocket = (payload: BugStepSocketResponse): BugStep => ({
  id: payload.id,
  bugId: payload.bugId,
  text: payload.text,
  stepNumber: payload.stepNumber,
  creatorUserId: payload.creatorUserId,
  createdAt: payload.createdAt,
  updatedAt: payload.updatedAt,
  attachments: payload.attachments
    ? payload.attachments.map(attachmentFromSocket)
    : null,
});

// Комментарий приезжает без вложений: `attachments: null` значит «с этим
// событием не приезжали», а не «их нет»; их дописывают события вложений.
export const commentFromSocket = (payload: CommentSocketResponse): Comment => ({
  id: payload.id,
  bugId: payload.bugId,
  text: payload.text,
  creatorUserId: payload.creatorUserId,
  creatorType: creatorTypeFromSocket(payload.creatorType),
  audience: commentAudienceFromSocket(payload.audience),
  createdAt: payload.createdAt,
  updatedAt: payload.updatedAt,
  attachments: null,
});

// Событие без вложений, но уже загруженные сохраняются.
export const commentUpdateFromSocket = (
  existing: Comment,
  payload: CommentSocketResponse
): Comment => ({
  ...commentFromSocket(payload),
  attachments: existing.attachments,
});

/** Статус приходит числом и может отсутствовать — «не менять». */
export const bugStatusPatchFromSocket = (
  value: number | null | undefined,
  current: BugClientEntity["status"]
): BugClientEntity["status"] =>
  value === null || value === undefined ? current : bugStatusFromSocket(value);

export const reportLinkFromSocket = (
  payload: ReportLinkSocketResponse
): ReportLink => ({
  id: payload.id,
  reportId: payload.reportId,
  link: payload.link,
  name: payload.name,
  createdAt: payload.createdAt,
  updatedAt: payload.updatedAt,
});

// `reportId` берётся из открытого репорта (в payload'е его нет): в сторе это
// alias, по которому группируются баги. Вложенные коллекции едут своими событиями.
export const bugFromSocket = (
  payload: CreateBugSocketResponse,
  reportId: string
): BugClientEntity => ({
  id: payload.id,
  reportId,
  title: payload.title,
  receive: payload.receive,
  expect: payload.expect,
  creatorUserId: payload.creatorUserId,
  creatorType: creatorTypeFromSocket(payload.creatorType),
  createdAt: payload.createdAt,
  updatedAt: payload.updatedAt,
  status: bugStatusFromSocket(payload.status),
  attachments: null,
  comments: null,
  clientId: payload.id,
  isLocalOnly: false,
});
