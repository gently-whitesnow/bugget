import { reportsApi } from "@/shared/api";
import type { SearchRequestQueryParams, SearchResponse } from "./contracts";

export const searchReports = async (
  query: SearchRequestQueryParams
): Promise<SearchResponse | void> => {
  try {
    return await reportsApi.searchReports(query);
  } catch (error) {
    console.error(error);
  }
};
