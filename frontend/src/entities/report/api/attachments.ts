import { appApi } from "@/shared/api";
import type { AttachmentResponse } from "./contracts";

type UploadAttachmentParameters = {
  reportId: string;
  bugId: number;
  attachType: number;
  file: File;
};

type RenameAttachmentParameters = {
  reportId: string;
  bugId: number;
  attachmentId: number;
  fileName: string;
};

type RenameCommentAttachmentParameters = RenameAttachmentParameters & {
  commentId: number;
};

type RenameBugStepAttachmentParameters = RenameAttachmentParameters & {
  stepId: number;
};

export const uploadAttachment = async (params: UploadAttachmentParameters) => {
  try {
    const { reportId, bugId, file, attachType } = params;
    const formData = new FormData();
    formData.append("file", file);

    const { data } = await appApi.post(
      `/v2/reports/${reportId}/bugs/${bugId}/attachments?attachType=${attachType}`,
      formData,
      {
        headers: {
          "Content-Type": "multipart/form-data",
        },
      }
    );
    return data;
  } catch (error) {
    console.error("Ошибка при загрузке файла:", error);
    throw new Error("Не удалось загрузить файл");
  }
};

export const deleteBugAttachment = async (
  reportId: string,
  bugId: number,
  attachmentId: number
): Promise<void> => {
  await appApi.delete(
    `/v2/reports/${reportId}/bugs/${bugId}/attachments/${attachmentId}`
  );
};

export const renameBugAttachment = async ({
  reportId,
  bugId,
  attachmentId,
  fileName,
}: RenameAttachmentParameters): Promise<AttachmentResponse> => {
  const { data } = await appApi.patch<AttachmentResponse>(
    `/v2/reports/${reportId}/bugs/${bugId}/attachments/${attachmentId}`,
    { fileName }
  );
  return data;
};

export const createCommentAttachment = async (
  reportId: string,
  bugId: number,
  commentId: number,
  file: File
): Promise<AttachmentResponse> => {
  const formData = new FormData();
  formData.append("file", file);
  const { data } = await appApi.post<AttachmentResponse>(
    `/v2/reports/${reportId}/bugs/${bugId}/comments/${commentId}/attachments`,
    formData,
    { headers: { "Content-Type": "multipart/form-data" } }
  );
  return data;
};

export const deleteCommentAttachment = async (
  reportId: string,
  bugId: number,
  commentId: number,
  attachmentId: number
): Promise<void> => {
  await appApi.delete(
    `/v2/reports/${reportId}/bugs/${bugId}/comments/${commentId}/attachments/${attachmentId}`
  );
};

export const renameCommentAttachment = async ({
  reportId,
  bugId,
  commentId,
  attachmentId,
  fileName,
}: RenameCommentAttachmentParameters): Promise<AttachmentResponse> => {
  const { data } = await appApi.patch<AttachmentResponse>(
    `/v2/reports/${reportId}/bugs/${bugId}/comments/${commentId}/attachments/${attachmentId}`,
    { fileName }
  );
  return data;
};

export const createBugStepAttachment = async (
  reportId: string,
  bugId: number,
  stepId: number,
  file: File
): Promise<AttachmentResponse> => {
  const formData = new FormData();
  formData.append("file", file);
  const { data } = await appApi.post<AttachmentResponse>(
    `/v2/reports/${reportId}/bugs/${bugId}/steps/${stepId}/attachments`,
    formData,
    { headers: { "Content-Type": "multipart/form-data" } }
  );
  return data;
};

export const deleteBugStepAttachment = async (
  reportId: string,
  bugId: number,
  stepId: number,
  attachmentId: number
): Promise<void> => {
  await appApi.delete(
    `/v2/reports/${reportId}/bugs/${bugId}/steps/${stepId}/attachments/${attachmentId}`
  );
};

export const renameBugStepAttachment = async ({
  reportId,
  bugId,
  stepId,
  attachmentId,
  fileName,
}: RenameBugStepAttachmentParameters): Promise<AttachmentResponse> => {
  const { data } = await appApi.patch<AttachmentResponse>(
    `/v2/reports/${reportId}/bugs/${bugId}/steps/${stepId}/attachments/${attachmentId}`,
    { fileName }
  );
  return data;
};
