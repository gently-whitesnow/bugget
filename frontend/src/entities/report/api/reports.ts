import { reportsApi } from "@/shared/api";
import type { ReportStatuses } from "@/shared/config";
import type {
  CreateReportRequest,
  CreateReportResponse,
  ListReportsQuery,
  ListReportsResponse,
  PatchReportRequest,
  PatchReportResponse,
  ReportResponse,
  LegacyReportResolveResponse,
} from "./contracts";

// Транспорт живёт в `shared/api/reports`; здесь только логирование для отладки.

export const fetchReport = async (id: string): Promise<ReportResponse> => {
  try {
    return await reportsApi.getReport(id);
  } catch (error) {
    console.error(error);
    throw error;
  }
};

export const createReport = async (
  request: CreateReportRequest
): Promise<CreateReportResponse> => {
  try {
    return await reportsApi.createReport(request);
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
    return await reportsApi.patchReport(id, request);
  } catch (error) {
    console.error(error);
    throw error;
  }
};

/** Реализация одна — `reportsApi.listReports`; здесь умолчания дашборда. */
export const fetchReportsList = async (
  userId: string | null = null,
  teamId: string | null = null,
  reportStatuses: ReportStatuses[] | null = null,
  skip: number = 0,
  take: number = 10
): Promise<ListReportsResponse> => {
  try {
    const query: ListReportsQuery = {
      // Пустой фильтр по пользователю и команде не отправляется, как и раньше.
      userId: userId || undefined,
      teamId: teamId || undefined,
      reportStatuses,
      skip,
      take,
    };

    return await reportsApi.listReports(query);
  } catch (error) {
    console.error(error);
    throw error;
  }
};

export const resolveLegacyReport = async (
  legacyId: string
): Promise<LegacyReportResolveResponse> => {
  try {
    return await reportsApi.resolveLegacyReport(legacyId);
  } catch (error) {
    console.error(error);
    throw error;
  }
};
