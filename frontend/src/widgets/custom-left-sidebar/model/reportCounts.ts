import { createEffect, createStore } from "effector";
import { reportsApi } from "@/shared/api";
import type { WireInt64 } from "@/shared/api";

/**
 * Батч-счётчики репортов для вкладок сайдбара. Ответ — массив
 * `counts: [{ key, count }]`, а не карта (ADR-0009): ключ среза задаёт
 * клиент, и интерсептор регистра переписал бы его как имя поля.
 * `count` — `Int64String`: сравнивать через `compareWireInt64`, не `Number`.
 */

export type ReportCountsScope = reportsApi.ReportCountsBody["scopes"][number];

const fetchReportCountsBatch = async (
  scopes: ReportCountsScope[]
): Promise<Record<string, WireInt64>> => {
  const { counts } = await reportsApi.countReportsBatch({ scopes });

  return Object.fromEntries(counts.map(({ key, count }) => [key, count]));
};

export const fetchReportCountsFx = createEffect<
  ReportCountsScope[],
  Record<string, WireInt64>
>(fetchReportCountsBatch);

export const $reportCounts = createStore<Record<string, WireInt64>>({}).on(
  fetchReportCountsFx.doneData,
  (state, payload) => ({ ...state, ...payload })
);
