import { appApi } from "@/shared/api";
import type { KaitenBoardResponse } from "./contracts";

export async function searchKaitenBoards(
  query?: string
): Promise<KaitenBoardResponse[]> {
  try {
    const params = query ? { query } : {};
    const { data } = await appApi.get<KaitenBoardResponse[]>(
      "/v1/external/kaiten/boards",
      { params }
    );
    return data;
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
    const { data } = await appApi.post<KaitenBoardResponse[]>(
      "/v1/external/kaiten/boards/batch-get",
      { ids }
    );
    return data;
  } catch (error) {
    console.error(error);
    throw error;
  }
}
