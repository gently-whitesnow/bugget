import { appApi } from "@/shared/api";
import type { ReportLink, ReportLinkDto } from "../model/types";

export const createReportLink = async (
  reportId: string,
  dto: ReportLinkDto
): Promise<ReportLink> => {
  const { data } = await appApi.post<ReportLink>(
    `/v2/reports/${reportId}/links`,
    dto
  );
  return data;
};

export const updateReportLink = async (
  reportId: string,
  linkId: number,
  dto: ReportLinkDto
): Promise<ReportLink> => {
  const { data } = await appApi.put<ReportLink>(
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
