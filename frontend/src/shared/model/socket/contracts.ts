import { ReportStatuses } from "@/shared/config";

/**
 * Payload'ы realtime-событий.
 *
 * Это отдельный контракт: он описан в `specs/contracts/events.yaml`, ничего из
 * него не генерируется, форму сообщений менять нельзя (ADR-0007). Поэтому типы
 * здесь свои и HTTP-схемы модуля `reports` сюда не подставляются, даже когда
 * формы сегодня совпадают: изменение OpenAPI не должно молча менять типы
 * realtime-пути. Перевод payload'а в сущность стора делают явные адаптеры
 * (`entities/report/lib/fromSocket.ts`).
 *
 * Сообщения SignalR приходят в camelCase и конверсию регистра не проходят.
 */

export type AttachmentSocketResponse = {
  id: number;
  entityId: number;
  attachType: number;
  createdAt: string;
  creatorUserId: string;
  fileName: string;
  hasPreview: boolean;
};

export type BugStepSocketResponse = {
  id: number;
  bugId: number;
  text: string;
  stepNumber: number;
  creatorUserId: string;
  createdAt: string;
  updatedAt: string;
  attachments: AttachmentSocketResponse[] | null;
};

/**
 * `reportId` — число: по SignalR уходит `ReportLinkDbModel` с `int ReportId`,
 * то же значение и того же типа, что в HTTP-ответе (`ReportLink` в контракте
 * модуля `reports`, снимок `v2.reports.get`). Раньше здесь стояла строка —
 * зеркало SignalR расходилось с фактическим проводом.
 */
export type ReportLinkSocketResponse = {
  id: number;
  reportId: number;
  link: string;
  name: string;
  createdAt: string;
  updatedAt: string;
};

export type PatchReportSocketResponse = {
  title?: string | null;
  status?: ReportStatuses | null;
  responsibleUserId?: string | null;
  pastResponsibleUserId?: string | null;
  updatedAt: string;
};

export type PatchBugSocketResponse = {
  title?: string | null;
  receive?: string | null;
  expect?: string | null;
  status?: number | null;
};

/**
 * `ReceiveBugCreate` публикует `BugSummaryDbModel`, где `CreatorType` обязателен
 * (`backend/Bugget.Entities/DbModels/Bug/BugSummaryDbModel.cs`). Зеркало это поле
 * теряло, и в сторе на его месте стояла константа — расхождение, замаскированное
 * значением по умолчанию.
 */
export type CreateBugSocketResponse = {
  id: number;
  title: string | null;
  receive: string | null;
  expect: string | null;
  createdAt: string;
  updatedAt: string;
  creatorUserId: string;
  creatorType: number;
  status: number;
};

export type CommentSocketResponse = {
  id: number;
  bugId: number;
  text: string;
  creatorType: number;
  audience: number;
  creatorUserId: string;
  createdAt: string;
  updatedAt: string;
};

export enum SocketEvent {
  ReportParticipant = "ReceiveReportParticipant",
  ReportPatch = "ReceiveReportPatch",
  ReportLinkCreate = "ReceiveReportLinkCreate",
  ReportLinkUpdate = "ReceiveReportLinkUpdate",
  ReportLinkDelete = "ReceiveReportLinkDelete",
  BugPatch = "ReceiveBugPatch",
  BugCreate = "ReceiveBugCreate",
  CommentAttachmentCreate = "ReceiveCommentAttachmentCreate",
  BugAttachmentCreate = "ReceiveBugAttachmentCreate",
  BugStepAttachmentCreate = "ReceiveBugStepAttachmentCreate",
  CommentAttachmentChanged = "ReceiveCommentAttachmentChanged",
  BugAttachmentChanged = "ReceiveBugAttachmentChanged",
  BugStepAttachmentChanged = "ReceiveBugStepAttachmentChanged",
  CommentAttachmentDelete = "ReceiveCommentAttachmentDelete",
  BugAttachmentDelete = "ReceiveBugAttachmentDelete",
  BugStepAttachmentDelete = "ReceiveBugStepAttachmentDelete",
  CommentCreate = "ReceiveCommentCreate",
  CommentDelete = "ReceiveCommentDelete",
  CommentUpdate = "ReceiveCommentUpdate",
  BugStepCreate = "ReceiveBugStepCreate",
  BugStepPatch = "ReceiveBugStepPatch",
  BugStepsOrderUpdate = "ReceiveBugStepsOrderUpdate",
  BugStepDelete = "ReceiveBugStepDelete",
}

export type SocketPayload = {
  [SocketEvent.ReportPatch]: PatchReportSocketResponse;
  [SocketEvent.ReportParticipant]: string;
  [SocketEvent.ReportLinkCreate]: ReportLinkSocketResponse;
  [SocketEvent.ReportLinkUpdate]: ReportLinkSocketResponse;
  [SocketEvent.ReportLinkDelete]: number;
  [SocketEvent.BugPatch]: { bugId: number; patch: PatchBugSocketResponse };
  [SocketEvent.BugCreate]: CreateBugSocketResponse;
  [SocketEvent.CommentAttachmentCreate]: AttachmentSocketResponse;
  [SocketEvent.BugAttachmentCreate]: AttachmentSocketResponse;
  [SocketEvent.BugStepAttachmentCreate]: AttachmentSocketResponse;
  [SocketEvent.CommentAttachmentChanged]: AttachmentSocketResponse;
  [SocketEvent.BugAttachmentChanged]: AttachmentSocketResponse;
  [SocketEvent.BugStepAttachmentChanged]: AttachmentSocketResponse;
  [SocketEvent.CommentAttachmentDelete]: {
    id: number;
    entityId: number;
    attachType: number;
  };
  [SocketEvent.BugAttachmentDelete]: {
    id: number;
    entityId: number;
    attachType: number;
  };
  [SocketEvent.BugStepAttachmentDelete]: {
    id: number;
    entityId: number;
    attachType: number;
  };
  [SocketEvent.CommentCreate]: CommentSocketResponse;
  [SocketEvent.CommentDelete]: { bugId: number; commentId: number };
  [SocketEvent.CommentUpdate]: CommentSocketResponse;
  [SocketEvent.BugStepCreate]: BugStepSocketResponse;
  [SocketEvent.BugStepPatch]: { bugId: number; step: BugStepSocketResponse };
  [SocketEvent.BugStepsOrderUpdate]: {
    bugId: number;
    steps: BugStepSocketResponse[];
  };
  [SocketEvent.BugStepDelete]: { bugId: number; stepId: number };
};

export const customParsers: Partial<
  Record<SocketEvent, (...args: unknown[]) => SocketPayload[SocketEvent]>
> = {
  [SocketEvent.BugPatch]: (...args: unknown[]) => {
    const [bugId, patch] = args as [number, PatchBugSocketResponse];

    return { bugId, patch };
  },

  [SocketEvent.CommentDelete]: (...args: unknown[]) => {
    const [bugId, commentId] = args as [number, number];

    return { bugId, commentId };
  },

  [SocketEvent.BugStepPatch]: (...args: unknown[]) => {
    const [bugId, step] = args as [number, BugStepSocketResponse];

    return { bugId, step };
  },

  [SocketEvent.BugStepsOrderUpdate]: (...args: unknown[]) => {
    const [bugId, steps] = args as [number, BugStepSocketResponse[]];

    return { bugId, steps };
  },

  [SocketEvent.BugStepDelete]: (...args: unknown[]) => {
    const [bugId, stepId] = args as [number, number];

    return { bugId, stepId };
  },

  [SocketEvent.BugAttachmentDelete]: (...args: unknown[]) => {
    const [id, entityId, attachType] = args as [number, number, number];

    return { id, entityId, attachType };
  },

  [SocketEvent.CommentAttachmentDelete]: (...args: unknown[]) => {
    const [id, entityId, attachType] = args as [number, number, number];

    return { id, entityId, attachType };
  },

  [SocketEvent.BugStepAttachmentDelete]: (...args: unknown[]) => {
    const [id, entityId, attachType] = args as [number, number, number];

    return { id, entityId, attachType };
  },
};
