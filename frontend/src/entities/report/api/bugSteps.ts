import { reportsApi } from "@/shared/api";
import type {
  BugStepOrderRequest,
  BugStepRequest,
  BugStepResponse,
} from "./contracts";

/** С файлами шаг и вложения создаются одной транзакцией (ADR-0015). */
export const createBugStep = async (
  reportId: string,
  bugId: number,
  payload: BugStepRequest,
  files: File[] = []
): Promise<BugStepResponse> =>
  files.length === 0
    ? reportsApi.createBugStep(reportId, bugId, payload)
    : reportsApi.createBugStepWithAttachments(
        reportId,
        bugId,
        payload.text,
        files
      );

export const patchBugStep = async (
  reportId: string,
  bugId: number,
  stepId: number,
  payload: BugStepRequest
): Promise<BugStepResponse> =>
  reportsApi.patchBugStep(reportId, bugId, stepId, payload);

export const deleteBugStep = async (
  reportId: string,
  bugId: number,
  stepId: number
): Promise<void> => reportsApi.deleteBugStep(reportId, bugId, stepId);

export const updateBugStepsOrder = async (
  reportId: string,
  bugId: number,
  payload: BugStepOrderRequest
): Promise<BugStepResponse[]> =>
  reportsApi.updateBugStepsOrder(reportId, bugId, payload);
