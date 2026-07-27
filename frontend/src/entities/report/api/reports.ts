import { appApi } from "@/shared/api";
import type { ListReportsResponse } from "@/shared/api";
import type {
  CreateReportRequest,
  CreateReportResponse,
  PatchReportRequest,
  PatchReportResponse,
  ReportResponse,
  LegacyReportResolveResponse,
} from "./contracts";

export const fetchReport = async (id: string): Promise<ReportResponse> => {
  try {
    const { data } = await appApi.get<ReportResponse>(`/v2/reports/${id}`);
    return data;
  } catch (error) {
    console.error(error);
    throw error;
  }
};

export const createReport = async (
  request: CreateReportRequest
): Promise<CreateReportResponse> => {
  try {
    const { data } = await appApi.post<CreateReportResponse>(
      "/v2/reports",
      request
    );
    return data;
  } catch (error) {
    console.error(error);
    throw error;
  }
};

export const patchReport = async (
  id: string,
  request: PatchReportRequest
): Promise<PatchReportResponse> => {
  try {
    const { data } = await appApi.patch<PatchReportResponse>(
      `/v2/reports/${id}`,
      request
    );
    return data;
  } catch (error) {
    console.error(error);
    throw error;
  }
};

export const fetchReportsList = async (
  userId: string | null = null,
  teamId: string | null = null,
  reportStatuses: number[] | null = null,
  skip: number = 0,
  take: number = 10
): Promise<ListReportsResponse> => {
  try {
    const searchParams = new URLSearchParams();
    if (userId) searchParams.append("userId", userId);
    if (teamId) searchParams.append("teamId", teamId);
    if (reportStatuses) {
      for (const status of reportStatuses) {
        searchParams.append("reportStatuses", String(status));
      }
    }
    searchParams.append("skip", String(skip));
    searchParams.append("take", String(take));

    const { data } = await appApi.get<ListReportsResponse>(
      `/v2/reports?${searchParams.toString()}`
    );
    return data;
  } catch (error) {
    console.error(error);
    throw error;
  }
};

export const resolveLegacyReport = async (
  legacyId: string
): Promise<LegacyReportResolveResponse> => {
  try {
    const { data } = await appApi.get<LegacyReportResolveResponse>(
      `/v2/reports/legacy/${legacyId}`
    );
    return data;
  } catch (error) {
    console.error(error);
    throw error;
  }
};
