import { appApi, buildQueryString } from "@/shared/api";
import type { SearchRequestQueryParams, SearchResponse } from "./contracts";

export const searchReports = async (
  query: SearchRequestQueryParams
): Promise<SearchResponse | void> => {
  try {
    const { data } = await appApi.get<SearchResponse>(
      `/v1/reports/search?${buildQueryString(query)}`
    );
    return data;
  } catch (error) {
    console.error(error);
  }
};
