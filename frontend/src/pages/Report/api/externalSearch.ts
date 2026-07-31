import { externalApi } from "@/shared/api";
import type {
  ExternalSearchApplyRequest,
  ExternalSearchResponse,
} from "./contracts";

/**
 * Поиск по внешним источникам глазами страницы репорта. Транспорт живёт в
 * операциях модуля (`shared/api/external`) — здесь остаётся только пагинация по
 * умолчанию, с которой ходит эта страница: она часть её поведения, а не контракта.
 */
export const searchExternal = async (
  query: string,
  skip = 0,
  take = 7
): Promise<ExternalSearchResponse> =>
  externalApi.searchExternal({ query, skip, take });

export const applyExternalSearchResult = async (
  payload: ExternalSearchApplyRequest
): Promise<void> => {
  await externalApi.applyExternalSearchResult(payload);
};
