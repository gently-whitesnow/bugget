import { sample } from "effector";

import { CreatorTypes } from "@/shared/config";

import {
  createReportFx,
  $creatorUserIdStore,
  $pastResponsibleUserIdStore,
  $reportIdStore,
  $responsibleUserIdStore,
  $usersStore,
  fetchUsersFx,
  clearReport,
  getReportFx,
  updateReportPathIdEvent,
} from "@/entities/report";
import type { ExternalSearchItem } from "./api/contracts";
import type { ReportResponse } from "@/entities/report";
import { $authUserStore } from "@/entities/user";
import {
  initSocketFx,
  connectionRestored,
  connectionReconnected,
} from "@/shared/model";
import { setBugStepsEvent } from "./@x/bug-step";
import { setBugsEvent } from "@/entities/report";
import {
  setCommentsByBugIdEvent,
  createCommentSocketEvent,
} from "./@x/comment";
import {
  setReportLinksEvent,
  resetReportLinksEvent,
  createLinkEvent,
  updateLinkEvent,
  deleteLinkEvent,
  createReportLinkFx,
  updateReportLinkFx,
  deleteReportLinkFx,
} from "./@x/report-link";

import {
  $selectedExternalSearchItemStore,
  applyExternalSearchResultFx,
} from "./model";

sample({
  clock: createReportFx.doneData,
  source: $selectedExternalSearchItemStore,
  filter: (item): item is ExternalSearchItem => item !== null,
  fn: (item, report) => ({
    id: item!.id,
    source: item!.source,
    reportId: report.id,
  }),
  target: applyExternalSearchResultFx,
});

sample({
  clock: updateReportPathIdEvent,
  source: $authUserStore,
  filter: (user, reportPath) => reportPath === null && user.id !== undefined,
  fn: (user) => user.id,
  target: [
    $creatorUserIdStore,
    $responsibleUserIdStore,
    $pastResponsibleUserIdStore,
  ],
});

// Initialize socket when reportId is available
sample({
  clock: $reportIdStore,
  filter: (id) => id !== null,
  target: initSocketFx,
});

/**
 * Пока связи не было, серверные события (новые комментарии и прочее) до нас не
 * доходили. Перезабираем репорт целиком — он приходит вместе с комментариями.
 */
sample({
  clock: [connectionRestored, connectionReconnected],
  source: $reportIdStore,
  filter: (id): id is string => id !== null,
  target: getReportFx,
});

// Set bug steps when report is loaded
sample({
  clock: getReportFx.doneData,
  fn: (report: ReportResponse) => {
    if (!report.bugs) return [];

    return report.bugs.map((bug) => ({
      bugId: bug.id,
      steps: bug.steps || [],
    }));
  },
  target: setBugStepsEvent,
});

// Set bugs when report is loaded
sample({
  clock: getReportFx.doneData,
  fn: (report: ReportResponse) => ({
    reportId: report.id,
    bugs: (report.bugs || []).map((bug) => ({
      ...bug,
      clientId: bug.id,
      isLocalOnly: false,
    })),
  }),
  target: setBugsEvent,
});

// Set comments when report is loaded
sample({
  clock: getReportFx.doneData,
  fn: (report: ReportResponse) => {
    if (!report.bugs) return [];

    const allComments = [];
    for (const bug of report.bugs) {
      if (bug.comments && bug.comments.length > 0) {
        allComments.push({
          bugId: bug.id,
          comments: bug.comments.map((comment) => ({
            id: comment.id,
            bugId: bug.id,
            text: comment.text,
            creatorUserId: comment.creatorUserId,
            creatorType: comment.creatorType,
            audience: comment.audience ?? 0,
            createdAt: comment.createdAt,
            updatedAt: comment.updatedAt,
            attachments: comment.attachments || null,
          })),
        });
      }
    }
    return allComments;
  },
  target: setCommentsByBugIdEvent,
});

// Fetch comment creator users after report is loaded
sample({
  clock: getReportFx.doneData,
  fn: (report: ReportResponse) => {
    const ids = new Set<string>();
    report.bugs?.forEach((bug) => {
      bug.comments?.forEach((comment) => {
        if (
          comment.creatorUserId &&
          comment.creatorType !== CreatorTypes.TG_BETA_TESTER
        ) {
          ids.add(comment.creatorUserId);
        }
      });
    });
    return [...ids];
  },
  target: fetchUsersFx,
});

// Fetch user for new comment from socket
sample({
  clock: createCommentSocketEvent,
  source: $usersStore,
  filter: (users, comment) =>
    !!comment.creatorUserId &&
    comment.creatorType !== CreatorTypes.TG_BETA_TESTER &&
    !users[comment.creatorUserId],
  fn: (_, comment) => [comment.creatorUserId],
  target: fetchUsersFx,
});

// Set report links when report is loaded
sample({
  clock: getReportFx.doneData,
  fn: (report: ReportResponse) => report.links || [],
  target: setReportLinksEvent,
});

// Reset report links when report is cleared
sample({
  clock: clearReport,
  target: resetReportLinksEvent,
});

// Create report link
sample({
  clock: createLinkEvent,
  source: $reportIdStore,
  filter: (reportId) => reportId !== null,
  fn: (reportId, dto) => ({ reportId: reportId as string, dto }),
  target: createReportLinkFx,
});

// Update report link
sample({
  clock: updateLinkEvent,
  source: $reportIdStore,
  filter: (reportId) => reportId !== null,
  fn: (reportId, { linkId, dto }) => ({
    reportId: reportId as string,
    linkId,
    dto,
  }),
  target: updateReportLinkFx,
});

// Delete report link
sample({
  clock: deleteLinkEvent,
  source: $reportIdStore,
  filter: (reportId) => reportId !== null,
  fn: (reportId, linkId) => ({ reportId: reportId as string, linkId }),
  target: deleteReportLinkFx,
});
