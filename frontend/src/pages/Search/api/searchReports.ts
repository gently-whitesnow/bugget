import { appApi } from "@/shared/api";
import type { SearchResponse } from "./contracts";

export const searchReports = async (
  searchParams: string
): Promise<SearchResponse | void> => {
  try {
    const { data } = await appApi.get<SearchResponse>(
      `/v1/reports/search?${searchParams}`
    );
    return data;
  } catch (error) {
    console.error(error);
  }
};
