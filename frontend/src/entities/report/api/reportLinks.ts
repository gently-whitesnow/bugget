import { reportsApi } from "@/shared/api";
import type { ReportLinkRequest, ReportLinkResponse } from "./contracts";

export const createReportLink = async (
  reportId: string,
  dto: ReportLinkRequest
): Promise<ReportLinkResponse> => reportsApi.createReportLink(reportId, dto);

export const updateReportLink = async (
  reportId: string,
  linkId: number,
  dto: ReportLinkRequest
): Promise<ReportLinkResponse> =>
  reportsApi.updateReportLink(reportId, linkId, dto);

export const deleteReportLink = async (
  reportId: string,
  linkId: number
): Promise<void> => reportsApi.deleteReportLink(reportId, linkId);
