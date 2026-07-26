import { appApi } from "@/shared/api";
import type {
  ExternalSearchApplyRequest,
  ExternalSearchResponse,
} from "./contracts";

export const searchExternal = async (
  query: string,
  skip = 0,
  take = 7
): Promise<ExternalSearchResponse> => {
  const searchParams = new URLSearchParams();
  searchParams.append("query", query);
  searchParams.append("skip", String(skip));
  searchParams.append("take", String(take));

  const { data } = await appApi.get<ExternalSearchResponse>(
    `/v1/external/search?${searchParams.toString()}`
  );

  return data;
};

export const applyExternalSearchResult = async (
  payload: ExternalSearchApplyRequest
): Promise<void> => {
  await appApi.post(`/v1/external/search/apply`, payload);
};
