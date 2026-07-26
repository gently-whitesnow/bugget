import { createEffect, createStore } from "effector";
import { appApi } from "@/shared/api";

export type ReportCountsScope = {
  key: string;
  statuses?: number[];
  teamId?: string;
  creatorTypes?: number[];
};

type ReportCountsBatchResponse = {
  counts: Record<string, number>;
};

const fetchReportCountsBatch = async (scopes: ReportCountsScope[]) => {
  const { data } = await appApi.post<ReportCountsBatchResponse>(
    "/v2/reports/counts:batch",
    { scopes }
  );
  return data.counts;
};

export const fetchReportCountsFx = createEffect<
  ReportCountsScope[],
  Record<string, number>
>(fetchReportCountsBatch);

export const $reportCounts = createStore<Record<string, number>>({}).on(
  fetchReportCountsFx.doneData,
  (state, payload) => ({ ...state, ...payload })
);
