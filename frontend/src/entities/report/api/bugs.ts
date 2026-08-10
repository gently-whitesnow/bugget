import { reportsApi } from "@/shared/api";
import type {
  CreateBugRequest,
  CreateBugResponse,
  PatchBugRequest,
  PatchBugResponse,
} from "./contracts";

export const createBug = async (
  reportId: string,
  request: CreateBugRequest
): Promise<CreateBugResponse> => reportsApi.createBug(reportId, request);

export const updateBug = async (
  reportId: string,
  bugId: number,
  request: PatchBugRequest
): Promise<PatchBugResponse> => reportsApi.patchBug(reportId, bugId, request);
