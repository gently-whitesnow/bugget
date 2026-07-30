import { appApi, buildQueryString } from "@/shared/api";
import type {
  AttachmentRenameRequest,
  AttachmentResponse,
  AttachmentUploadForm,
  UploadAttachmentQuery,
} from "./contracts";

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

/**
 * Имя поля multipart — из схемы `AttachmentUpload`. Тело multipart регистр не
 * конвертирует (ADR-0009), поэтому имя обязано совпадать с контрактом дословно;
 * здесь это держит компилятор, а не внимательность на ревью.
 */
const attachmentFileField: keyof AttachmentUploadForm = "file";

const attachmentFormData = (file: File): FormData => {
  const formData = new FormData();
  formData.append(attachmentFileField, file);
  return formData;
};

const multipartHeaders = {
  headers: { "Content-Type": "multipart/form-data" },
};

export const uploadAttachment = async (
  params: UploadAttachmentParameters
): Promise<AttachmentResponse> => {
  try {
    const { reportId, bugId, file, attachType } = params;
    const query: UploadAttachmentQuery = { attachType };

    const { data } = await appApi.post<AttachmentResponse>(
      `/v2/reports/${reportId}/bugs/${bugId}/attachments?${buildQueryString(
        query
      )}`,
      attachmentFormData(file),
      multipartHeaders
    );
    return data;
  } catch (error) {
    console.error("Ошибка при загрузке файла:", error);
    throw new Error("Не удалось загрузить файл", { cause: error });
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
  const request: AttachmentRenameRequest = { fileName };
  const { data } = await appApi.patch<AttachmentResponse>(
    `/v2/reports/${reportId}/bugs/${bugId}/attachments/${attachmentId}`,
    request
  );
  return data;
};

export const createCommentAttachment = async (
  reportId: string,
  bugId: number,
  commentId: number,
  file: File
): Promise<AttachmentResponse> => {
  const { data } = await appApi.post<AttachmentResponse>(
    `/v2/reports/${reportId}/bugs/${bugId}/comments/${commentId}/attachments`,
    attachmentFormData(file),
    multipartHeaders
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
  const request: AttachmentRenameRequest = { fileName };
  const { data } = await appApi.patch<AttachmentResponse>(
    `/v2/reports/${reportId}/bugs/${bugId}/comments/${commentId}/attachments/${attachmentId}`,
    request
  );
  return data;
};

export const createBugStepAttachment = async (
  reportId: string,
  bugId: number,
  stepId: number,
  file: File
): Promise<AttachmentResponse> => {
  const { data } = await appApi.post<AttachmentResponse>(
    `/v2/reports/${reportId}/bugs/${bugId}/steps/${stepId}/attachments`,
    attachmentFormData(file),
    multipartHeaders
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
  const request: AttachmentRenameRequest = { fileName };
  const { data } = await appApi.patch<AttachmentResponse>(
    `/v2/reports/${reportId}/bugs/${bugId}/steps/${stepId}/attachments/${attachmentId}`,
    request
  );
  return data;
};
