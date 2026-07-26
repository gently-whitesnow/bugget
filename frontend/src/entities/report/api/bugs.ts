import { appApi } from "@/shared/api";
import type {
  CreateBugRequest,
  CreateBugResponse,
  PatchBugRequest,
  PatchBugResponse,
} from "./contracts";

export const createBug = async (
  reportId: string,
  request: CreateBugRequest
): Promise<CreateBugResponse> => {
  const { data } = await appApi.post<CreateBugResponse>(
    `/v2/reports/${reportId}/bugs`,
    request
  );
  return data;
};

export const updateBug = async (
  reportId: string,
  bugId: number,
  request: PatchBugRequest
): Promise<PatchBugResponse> => {
  const { data } = await appApi.patch<PatchBugResponse>(
    `/v2/reports/${reportId}/bugs/${bugId}`,
    request
  );
  return data;
};
