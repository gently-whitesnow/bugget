import { AttachmentTypes, BugStatuses, ReportStatuses } from "@/shared/config";
import type { BugStep, ReportLink, ReportLinkDto } from "../model/types";

export type CreateReportRequest = {
  title: string; // required, min length 1, max length 128
};

export type CreateReportResponse = {
  id: string;
  title: string;
  status: ReportStatuses;
  responsibleUserId: string;
  pastResponsibleUserId: string;
  creatorUserId: string;
  creatorType: number;
  creatorTeamId?: string | null;
  createdAt: string;
  updatedAt: string;
};

/**
 * Полная карточка репорта — ответ `GET /v2/reports/{aliasId}`, и только он.
 * Форма списка живёт отдельно (`ListReportsResponse` в `shared/api/contracts`):
 * LIST не отдаёт `links`, `bugs[].attachments` и `bugs[].steps`.
 * Перевод самой карточки на сгенерированный `Report` — отдельный слайс.
 */
export type ReportResponse = {
  id: string;
  title: string;
  status: ReportStatuses;
  responsibleUserId: string;
  pastResponsibleUserId: string;
  creatorUserId: string;
  creatorType: number;
  creatorTeamId?: string | null;
  createdAt: string;
  updatedAt: string;
  participantsUserIds: string[];
  links: ReportLink[] | null;
  bugs: BugResponse[] | null;
  isExcludedFromAnalytics?: boolean;
};

export type BugResponse = {
  id: number;
  reportId: string;
  title: string | null;
  receive: string | null;
  expect: string | null;
  creatorUserId: string;
  creatorType: number;
  createdAt: string;
  updatedAt: string;
  status: BugStatuses;
  attachments: AttachmentResponse[] | null;
  comments: CommentResponse[] | null;
  steps: BugStepResponse[] | null;
};

export type AttachmentResponse = {
  id: number;
  entityId: number;
  attachType: AttachmentTypes;
  createdAt: string;
  creatorUserId: string;
  fileName: string;
  hasPreview: boolean;
};

export type CommentResponse = {
  id: number;
  bugId: number;
  text: string;
  creatorUserId: string;
  createdAt: string;
  updatedAt: string;
  creatorType: number;
  audience: number;
  attachments: AttachmentResponse[] | null;
};

export type BugStepResponse = BugStep & {
  attachments: AttachmentResponse[] | null;
};

export type PatchReportRequest = {
  title?: string | null;
  status?: ReportStatuses | null;
  responsibleUserId?: string | null;
  isExcludedFromAnalytics?: boolean | null;
};

export type PatchReportResponse = {
  id: number;
  title: string;
  status: ReportStatuses;
  responsibleUserId: string;
  pastResponsibleUserId: string;
  updatedAt: string;
};

export type LegacyReportResolveResponse = {
  teamId: string;
  teamReportId: number;
};

export type CreateBugRequest = {
  title?: string | null; // optional, max length 128
  receive?: string | null; // required, min length 1, max length 2048
  expect?: string | null; // required, min length 1, max length 2048
};

export type PatchBugRequest = {
  title?: string | null; // optional, max length 128
  receive?: string | null; // required, min length 1, max length 2048
  expect?: string | null; // required, min length 1, max length 2048
  status?: number | null;
};

export type CreateBugResponse = {
  id: number;
  title: string | null;
  receive: string | null;
  expect: string | null;
  createdAt: string;
  updatedAt: string;
  creatorUserId: string;
  status: number;
};

export type PatchBugResponse = {
  id: number;
  title: string | null;
  receive: string | null;
  expect: string | null;
  updatedAt: string;
  status: number;
};

export type BugStepRequest = {
  text: string;
};

export type BugStepOrderRequest = {
  stepIds: number[];
};

export type CreateCommentRequest = {
  text: string; // required, min length 1, max length 2048
  audience?: number; // 0 = Internal (default), 1 = External (пересылается тестеру)
};

export type UpdateCommentRequest = {
  text: string; // required, min length 1, max length 2048
};

export type ReportLinkRequest = ReportLinkDto;
export type ReportLinkResponse = ReportLink;
