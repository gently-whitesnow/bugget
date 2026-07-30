import { reportsApi } from "@/shared/api";
import type { AttachmentResponse } from "./contracts";

/**
 * Вложения бага, комментария и шага. Multipart, имя поля файла и `attachType`
 * в query описаны операциями контракта в `shared/api/reports`; здесь остаётся
 * форма аргументов, удобная моделям страницы.
 */

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

export const uploadAttachment = async ({
  reportId,
  bugId,
  attachType,
  file,
}: UploadAttachmentParameters): Promise<AttachmentResponse> => {
  try {
    return await reportsApi.createBugAttachment(
      reportId,
      bugId,
      attachType,
      file
    );
  } catch (error) {
    console.error("Ошибка при загрузке файла:", error);
    throw new Error("Не удалось загрузить файл", { cause: error });
  }
};

export const deleteBugAttachment = async (
  reportId: string,
  bugId: number,
  attachmentId: number
): Promise<void> =>
  reportsApi.deleteBugAttachment(reportId, bugId, attachmentId);

export const renameBugAttachment = async ({
  reportId,
  bugId,
  attachmentId,
  fileName,
}: RenameAttachmentParameters): Promise<AttachmentResponse> =>
  reportsApi.renameBugAttachment(reportId, bugId, attachmentId, { fileName });

export const createCommentAttachment = async (
  reportId: string,
  bugId: number,
  commentId: number,
  file: File
): Promise<AttachmentResponse> =>
  reportsApi.createCommentAttachment(reportId, bugId, commentId, file);

export const deleteCommentAttachment = async (
  reportId: string,
  bugId: number,
  commentId: number,
  attachmentId: number
): Promise<void> =>
  reportsApi.deleteCommentAttachment(reportId, bugId, commentId, attachmentId);

export const renameCommentAttachment = async ({
  reportId,
  bugId,
  commentId,
  attachmentId,
  fileName,
}: RenameCommentAttachmentParameters): Promise<AttachmentResponse> =>
  reportsApi.renameCommentAttachment(reportId, bugId, commentId, attachmentId, {
    fileName,
  });

export const createBugStepAttachment = async (
  reportId: string,
  bugId: number,
  stepId: number,
  file: File
): Promise<AttachmentResponse> =>
  reportsApi.createBugStepAttachment(reportId, bugId, stepId, file);

export const deleteBugStepAttachment = async (
  reportId: string,
  bugId: number,
  stepId: number,
  attachmentId: number
): Promise<void> =>
  reportsApi.deleteBugStepAttachment(reportId, bugId, stepId, attachmentId);

export const renameBugStepAttachment = async ({
  reportId,
  bugId,
  stepId,
  attachmentId,
  fileName,
}: RenameBugStepAttachmentParameters): Promise<AttachmentResponse> =>
  reportsApi.renameBugStepAttachment(reportId, bugId, stepId, attachmentId, {
    fileName,
  });
