import { createEffect, createStore } from "effector";
import { appApi } from "@/shared/api";
import type { components } from "@/shared/api/generated/reports";
import type { Camelized } from "@/shared/lib/types";

/**
 * Батч-счётчики репортов для вкладок сайдбара.
 *
 * Формы запроса и ответа выводятся из контракта: рукописного DTO здесь нет.
 * Ответ приходит массивом `counts: [{ key, count }]`, а не картой со свободными
 * ключами (ADR-0005): ключ среза задаёт клиент, и в объекте он был бы неотличим
 * от имени поля — интерсептор перекладывает имена полей по регистру и переписал
 * бы ключ вместе с ними. Значение `key` не преобразуется ни на одной стороне,
 * поэтому в карту стора оно кладётся дословно.
 */

type ReportsSchemas = components["schemas"];

export type ReportCountsScope = Camelized<ReportsSchemas["ReportCountsScope"]>;
type ReportCountsBatchResponse = Camelized<
  ReportsSchemas["ReportCountsBatchResponse"]
>;

const fetchReportCountsBatch = async (
  scopes: ReportCountsScope[]
): Promise<Record<string, number>> => {
  const { data } = await appApi.post<ReportCountsBatchResponse>(
    "/v2/reports/counts:batch",
    { scopes }
  );

  return Object.fromEntries(data.counts.map(({ key, count }) => [key, count]));
};

export const fetchReportCountsFx = createEffect<
  ReportCountsScope[],
  Record<string, number>
>(fetchReportCountsBatch);

export const $reportCounts = createStore<Record<string, number>>({}).on(
  fetchReportCountsFx.doneData,
  (state, payload) => ({ ...state, ...payload })
);
