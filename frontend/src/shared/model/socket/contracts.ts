import { ReportStatuses } from "@/shared/config";

type Attachment = {
  id: number;
  entityId: number;
  attachType: number;
  createdAt: string;
  creatorUserId: string;
  fileName: string;
  hasPreview: boolean;
};

type BugStep = {
  id: number;
  bugId: number;
  text: string;
  stepNumber: number;
  creatorUserId: string;
  createdAt: string;
  updatedAt: string;
  attachments: Attachment[] | null;
};

type ReportLink = {
  id: number;
  reportId: string;
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

export type CreateBugSocketResponse = {
  id: number;
  title: string | null;
  receive: string | null;
  expect: string | null;
  createdAt: string;
  updatedAt: string;
  creatorUserId: string;
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

export type AttachmentSocketResponse = {
  id: number;
  entityId: number;
  attachType: number;
  createdAt: string;
  creatorUserId: string;
  fileName: string;
  hasPreview: boolean;
};

export type BugStepResponse = BugStep;

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
  [SocketEvent.ReportLinkCreate]: ReportLink;
  [SocketEvent.ReportLinkUpdate]: ReportLink;
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
  [SocketEvent.BugStepCreate]: BugStepResponse;
  [SocketEvent.BugStepPatch]: { bugId: number; step: BugStepResponse };
  [SocketEvent.BugStepsOrderUpdate]: {
    bugId: number;
    steps: BugStepResponse[];
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
    const [bugId, step] = args as [number, BugStepResponse];

    return { bugId, step };
  },

  [SocketEvent.BugStepsOrderUpdate]: (...args: unknown[]) => {
    const [bugId, steps] = args as [number, BugStepResponse[]];

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
