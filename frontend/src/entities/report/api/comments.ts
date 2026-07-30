import { reportsApi } from "@/shared/api";
import type {
  CommentSummaryResponse,
  CreateCommentRequest,
  UpdateCommentRequest,
} from "./contracts";

/**
 * Создание и обновление комментария отдают `CommentSummary` — без `attachments`:
 * у только что созданного комментария вложений ещё нет, и контракт их не
 * обещает. Вложения приезжают отдельными ручками и своими событиями.
 */
export const createComment = async (
  reportId: string,
  bugId: number,
  request: CreateCommentRequest
): Promise<CommentSummaryResponse> =>
  reportsApi.createComment(reportId, bugId, request);

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
