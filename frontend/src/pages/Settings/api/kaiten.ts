import { externalApi } from "@/shared/api";
import type { KaitenBoardResponse } from "./contracts";

/**
 * Доски Kaiten глазами страницы настроек. Транспорт живёт в операциях модуля
 * (`shared/api/external`) — здесь остаётся только журналирование отказа, с
 * которым эта страница жила и до миграции.
 */

export async function searchKaitenBoards(
  query?: string
): Promise<KaitenBoardResponse[]> {
  try {
    return await externalApi.searchKaitenBoards(query);
  } catch (error) {
    console.error(error);
    throw error;
  }
}

export async function fetchKaitenBoards(
  ids: number[]
): Promise<KaitenBoardResponse[]> {
  try {
    if (ids.length === 0) return [];
    return await externalApi.batchGetKaitenBoards({ ids });
  } catch (error) {
    console.error(error);
    throw error;
  }
}
