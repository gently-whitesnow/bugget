import { reportsApi } from "@/shared/api";
import type {
  CommentResponse,
  CommentSummaryResponse,
  CreateCommentRequest,
  UpdateCommentRequest,
} from "./contracts";

/**
 * Без файлов создание отдаёт `CommentSummary` — без `attachments`. С файлами
 * комментарий и вложения создаются одной транзакцией (ADR-0015), и ответ —
 * полный `Comment`.
 */
export const createComment = async (
  reportId: string,
  bugId: number,
  request: CreateCommentRequest,
  files: File[] = []
): Promise<CommentSummaryResponse | CommentResponse> =>
  files.length === 0
    ? reportsApi.createComment(reportId, bugId, request)
    : reportsApi.createCommentWithAttachments(
        reportId,
        bugId,
        request.text,
        files,
        request.audience ?? undefined
      );

export const updateComment = async (
  reportId: string,
  bugId: number,
  commentId: number,
  request: UpdateCommentRequest
): Promise<CommentSummaryResponse> =>
  reportsApi.updateComment(reportId, bugId, commentId, request);

export const deleteComment = async (
  reportId: string,
  bugId: number,
  commentId: number
): Promise<void> => reportsApi.deleteComment(reportId, bugId, commentId);
