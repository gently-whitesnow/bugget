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

/**
 * Адаптеры realtime-payload → сущность стора.
 *
 * Два контракта независимы: HTTP описан в `specs/contracts/reports/openapi.yaml`
 * и генерируется, SignalR — в `events.yaml` и не генерируется (ADR-0007).
 * Типизировать realtime-событие HTTP-схемой нельзя даже когда формы совпадают:
 * тогда правка OpenAPI молча меняла бы realtime-путь. Шов между ними живёт здесь
 * и разъезжается компиляцией, а не в рантайме у заказчика.
 *
 * Адаптеры перечисляют поля по именам намеренно: пропавшее у payload'а поле —
 * ошибка компиляции здесь, а не `undefined` в сторе.
 */

export const attachmentFromSocket = (
  payload: AttachmentSocketResponse
): Attachment => ({
  id: payload.id,
  entityId: payload.entityId,
  attachType: payload.attachType,
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

/**
 * Комментарий приезжает без вложений (`CommentSummaryDbModel`), поэтому в сторе
 * у него `attachments: null` — «с этим событием вложения не приезжали», а не «их
 * нет». Дальше их дописывают события вложений.
 */
export const commentFromSocket = (payload: CommentSocketResponse): Comment => ({
  id: payload.id,
  bugId: payload.bugId,
  text: payload.text,
  creatorUserId: payload.creatorUserId,
  creatorType: payload.creatorType,
  audience: payload.audience,
  createdAt: payload.createdAt,
  updatedAt: payload.updatedAt,
  attachments: null,
});

/**
 * Обновление комментария. Событие тоже приходит без вложений, но здесь они уже
 * могут быть загружены, поэтому сохраняются: «не приехали с этим событием» — не
 * повод их потерять.
 */
export const commentUpdateFromSocket = (
  existing: Comment,
  payload: CommentSocketResponse
): Comment => ({
  ...commentFromSocket(payload),
  attachments: existing.attachments,
});

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

/**
 * Баг из события создания. `reportId` берётся не из payload'а (его там нет), а из
 * открытого репорта: в сторе это alias, по которому баги группируются. Вложения,
 * комментарии и шаги событие не приносит — они приезжают своими событиями.
 */
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
  creatorType: payload.creatorType,
  createdAt: payload.createdAt,
  updatedAt: payload.updatedAt,
  status: payload.status,
  attachments: null,
  comments: null,
  clientId: payload.id,
  isLocalOnly: false,
});
