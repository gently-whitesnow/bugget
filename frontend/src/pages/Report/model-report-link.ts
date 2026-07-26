import { createEffect, createEvent, createStore } from "effector";
import {
  createReportLink,
  updateReportLink,
  deleteReportLink,
} from "@/entities/report";
import type { ReportLink, ReportLinkDto } from "@/entities/report";

/**
 * Эффекты
 */
export const createReportLinkFx = createEffect<
  { reportId: string; dto: ReportLinkDto },
  ReportLink
>(async ({ reportId, dto }) => {
  return await createReportLink(reportId, dto);
});

export const updateReportLinkFx = createEffect<
  { reportId: string; linkId: number; dto: ReportLinkDto },
  ReportLink
>(async ({ reportId, linkId, dto }) => {
  return await updateReportLink(reportId, linkId, dto);
});

export const deleteReportLinkFx = createEffect<
  { reportId: string; linkId: number },
  { linkId: number }
>(async ({ reportId, linkId }) => {
  await deleteReportLink(reportId, linkId);
  return { linkId };
});

/**
 * События
 */
export const createLinkEvent = createEvent<ReportLinkDto>();
export const updateLinkEvent = createEvent<{
  linkId: number;
  dto: ReportLinkDto;
}>();
export const deleteLinkEvent = createEvent<number>();
export const setReportLinksEvent = createEvent<ReportLink[]>();

// Socket события
export const createLinkSocketEvent = createEvent<ReportLink>();
export const updateLinkSocketEvent = createEvent<ReportLink>();
export const deleteLinkSocketEvent = createEvent<number>();

/**
 * Стор
 */
export const $reportLinksStore = createStore<ReportLink[]>([])
  .on(setReportLinksEvent, (_, links) => links)
  .on(createReportLinkFx.doneData, (state, link) => [...state, link])
  .on(updateReportLinkFx.doneData, (state, updatedLink) =>
    state.map((link) => (link.id === updatedLink.id ? updatedLink : link))
  )
  .on(deleteReportLinkFx.doneData, (state, { linkId }) =>
    state.filter((link) => link.id !== linkId)
  )
  // Socket обновления
  .on(createLinkSocketEvent, (state, link) => {
    if (state.some((l) => l.id === link.id)) return state;
    return [...state, link];
  })
  .on(updateLinkSocketEvent, (state, updatedLink) =>
    state.map((link) => (link.id === updatedLink.id ? updatedLink : link))
  )
  .on(deleteLinkSocketEvent, (state, linkId) =>
    state.filter((link) => link.id !== linkId)
  );

export const resetReportLinksEvent = createEvent<void>();

$reportLinksStore.reset(resetReportLinksEvent);
