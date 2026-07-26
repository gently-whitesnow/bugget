import { appApi } from "@/shared/api";
import type {
  CommentResponse,
  CreateCommentRequest,
  UpdateCommentRequest,
} from "./contracts";

export const createComment = async (
  reportId: string,
  bugId: number,
  request: CreateCommentRequest
): Promise<CommentResponse> => {
  const { data } = await appApi.post<CommentResponse>(
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
): Promise<CommentResponse> => {
  const { data } = await appApi.put<CommentResponse>(
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
