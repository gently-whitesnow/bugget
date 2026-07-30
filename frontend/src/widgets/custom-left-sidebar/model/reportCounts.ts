import { createEffect, createStore } from "effector";
import { reportsApi } from "@/shared/api";

/**
 * Батч-счётчики репортов для вкладок сайдбара.
 *
 * Транспорт — операция `POST /v2/reports/counts:batch` из `shared/api/reports`;
 * формы запроса и ответа выведены из неё, рукописного DTO здесь нет. Ответ
 * приходит массивом `counts: [{ key, count }]`, а не картой со свободными
 * ключами (ADR-0009): ключ среза задаёт клиент, и в объекте он был бы неотличим
 * от имени поля — интерсептор перекладывает имена полей по регистру и переписал
 * бы ключ вместе с ними. Значение `key` не преобразуется ни на одной стороне,
 * поэтому в карту стора оно кладётся дословно.
 */

export type ReportCountsScope = reportsApi.ReportCountsBody["scopes"][number];

const fetchReportCountsBatch = async (
  scopes: ReportCountsScope[]
): Promise<Record<string, number>> => {
  const { counts } = await reportsApi.countReportsBatch({ scopes });

  return Object.fromEntries(counts.map(({ key, count }) => [key, count]));
};

export const fetchReportCountsFx = createEffect<
  ReportCountsScope[],
  Record<string, number>
>(fetchReportCountsBatch);

export const $reportCounts = createStore<Record<string, number>>({}).on(
  fetchReportCountsFx.doneData,
  (state, payload) => ({ ...state, ...payload })
);
