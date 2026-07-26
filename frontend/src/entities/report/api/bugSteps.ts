import { appApi } from "@/shared/api";
import type {
  BugStepOrderRequest,
  BugStepRequest,
  BugStepResponse,
} from "./contracts";

export const createBugStep = async (
  reportId: string,
  bugId: number,
  payload: BugStepRequest
): Promise<BugStepResponse> => {
  const { data } = await appApi.post<BugStepResponse>(
    `/v2/reports/${reportId}/bugs/${bugId}/steps`,
    payload
  );
  return data;
};

export const patchBugStep = async (
  reportId: string,
  bugId: number,
  stepId: number,
  payload: BugStepRequest
): Promise<BugStepResponse> => {
  const { data } = await appApi.patch<BugStepResponse>(
    `/v2/reports/${reportId}/bugs/${bugId}/steps/${stepId}`,
    payload
  );
  return data;
};

export const deleteBugStep = async (
  reportId: string,
  bugId: number,
  stepId: number
): Promise<void> => {
  await appApi.delete(`/v2/reports/${reportId}/bugs/${bugId}/steps/${stepId}`);
};

export const updateBugStepsOrder = async (
  reportId: string,
  bugId: number,
  payload: BugStepOrderRequest
): Promise<BugStepResponse[]> => {
  const { data } = await appApi.put<BugStepResponse[]>(
    `/v2/reports/${reportId}/bugs/${bugId}/steps/order`,
    payload
  );
  return data;
};
