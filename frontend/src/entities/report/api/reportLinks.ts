import { appApi } from "@/shared/api";
import type { ReportLinkRequest, ReportLinkResponse } from "./contracts";

export const createReportLink = async (
  reportId: string,
  dto: ReportLinkRequest
): Promise<ReportLinkResponse> => {
  const { data } = await appApi.post<ReportLinkResponse>(
    `/v2/reports/${reportId}/links`,
    dto
  );
  return data;
};

export const updateReportLink = async (
  reportId: string,
  linkId: number,
  dto: ReportLinkRequest
): Promise<ReportLinkResponse> => {
  const { data } = await appApi.put<ReportLinkResponse>(
    `/v2/reports/${reportId}/links/${linkId}`,
    dto
  );
  return data;
};

export const deleteReportLink = async (
  reportId: string,
  linkId: number
): Promise<void> => {
  await appApi.delete(`/v2/reports/${reportId}/links/${linkId}`);
};
