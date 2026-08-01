import { createEffect, createEvent, createStore, sample } from "effector";

import {
  applyExternalSearchResult,
  searchExternal,
} from "./api/externalSearch";
import type {
  ExternalSearchApplyRequest,
  ExternalSearchItem,
  ExternalSearchResponse,
} from "./api/contracts";
import { clearReport } from "@/entities/report";

export const searchExternalFx = createEffect<string, ExternalSearchResponse>(
  async (query) => {
    return await searchExternal(query);
  }
);

export const applyExternalSearchResultFx = createEffect<
  ExternalSearchApplyRequest,
  void
>(async (payload) => {
  return await applyExternalSearchResult(payload);
});

export const selectExternalSearchItemEvent = createEvent<ExternalSearchItem>();
export const clearExternalSearchResultsEvent = createEvent<void>();
export const clearExternalSelectionEvent = createEvent<void>();

export const $externalSearchResultsStore = createStore<ExternalSearchItem[]>([])
  .on(searchExternalFx.doneData, (_, data) => data.items)
  .reset(clearReport, clearExternalSearchResultsEvent);

// `total` внешнего поиска — канон Int64String: хранится строкой, сравнивается
// точно (`shared/lib/wireInt64`), `Number(...)` к нему не применяется.
export const $externalSearchTotalStore = createStore<string>("0")
  .on(searchExternalFx.doneData, (_, data) => data.total)
  .reset(clearReport, clearExternalSearchResultsEvent);

export const $selectedExternalSearchItemStore =
  createStore<ExternalSearchItem | null>(null)
    .on(selectExternalSearchItemEvent, (_, item) => item)
    .reset(clearReport, clearExternalSelectionEvent);

sample({
  clock: selectExternalSearchItemEvent,
  target: clearExternalSearchResultsEvent,
});

sample({
  clock: applyExternalSearchResultFx.done,
  target: clearExternalSelectionEvent,
});
