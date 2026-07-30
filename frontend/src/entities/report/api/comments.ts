import { appApi } from "@/shared/api";
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
): Promise<CommentSummaryResponse> => {
  const { data } = await appApi.post<CommentSummaryResponse>(
    `/v2/reports/${reportId}/bugs/${bugId}/comments`,
    request
  );
  return data;
};

export const updateComment = async (
  reportId: string,
  bugId: number,
  commentId: number,
  request: UpdateCommentRequest
): Promise<CommentSummaryResponse> => {
  const { data } = await appApi.put<CommentSummaryResponse>(
    `/v2/reports/${reportId}/bugs/${bugId}/comments/${commentId}`,
    request
  );
  return data;
};

export const deleteComment = async (
  reportId: string,
  bugId: number,
  commentId: number
): Promise<void> => {
  await appApi.delete(
    `/v2/reports/${reportId}/bugs/${bugId}/comments/${commentId}`
  );
};
