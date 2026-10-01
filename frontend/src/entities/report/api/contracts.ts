import type { reportsApi } from "@/shared/api";
import type {
  Attachment,
  BugStep,
  ReportLink,
  ReportLinkDto,
} from "../model/types";

/**
 * Все формы — типы операций из `shared/api/reports` (ADR-0009): сменилась схема
 * ответа операции — здесь перестало компилироваться. Рукописных DTO нет.
 */

export type CreateReportRequest = reportsApi.CreateReportBody;

export type CreateReportResponse = reportsApi.CreateReportResult;

/** Форма списка живёт отдельно: LIST не отдаёт `links` и `bugs[].steps`. */
export type ReportResponse = reportsApi.ReportResult;

export type BugResponse = NonNullable<ReportResponse["bugs"]>[number];

export type AttachmentResponse = Attachment;

export type CommentResponse = NonNullable<BugResponse["comments"]>[number];

/** Без `attachments`: у только что созданного комментария их ещё нет. */
export type CommentSummaryResponse = reportsApi.CommentResult;

export type BugStepResponse = BugStep;

export type PatchReportRequest = reportsApi.PatchReportBody;

/** `id` — alias, как и в URL. */
export type PatchReportResponse = reportsApi.PatchReportResult;

export type LegacyReportResolveResponse = reportsApi.LegacyReportResolveResult;

export type CreateBugRequest = reportsApi.CreateBugBody;

export type PatchBugRequest = reportsApi.PatchBugBody;

/** Без вложенных коллекций и без `reportId`. */
export type CreateBugResponse = reportsApi.CreateBugResult;

export type PatchBugResponse = reportsApi.PatchBugResult;

export type BugStepRequest = reportsApi.BugStepBody;

export type BugStepOrderRequest = reportsApi.BugStepsOrderBody;

export type CreateCommentRequest = reportsApi.CommentBody;

export type UpdateCommentRequest = reportsApi.CommentBody;

export type ReportLinkRequest = ReportLinkDto;

export type ReportLinkResponse = ReportLink;

export type ListReportsResponse = reportsApi.ListReportsResult;
export type ListReportsQuery = reportsApi.ListReportsQuery;
