import { combine, sample } from "effector";

import { createBugFx } from "./model-bug";
import { $reportIdStore, $bugsData } from "@/entities/report";
import { $authUserStore } from "@/entities/user";
import { BugStatuses } from "@/shared/config";
import type { BugFormData } from "@/entities/report";

import {
  $newBugStore,
  clearNewBugEvent,
  createBugOnServerEvent,
} from "./model-create-bug";

sample({
  source: $authUserStore,
  clock: createBugOnServerEvent,
  fn: (user, { reportId, receive, expect, clientId, title }) => {
    const data: Partial<BugFormData> = { status: BugStatuses.OPEN };
    if (title) data.title = title;
    if (receive.trim()) data.receive = receive.trim();
    if (expect.trim()) data.expect = expect.trim();
    return { reportId, data: { ...data, creatorUserId: user.id }, clientId };
  },
  target: createBugFx,
});

sample({
  clock: $reportIdStore,
  target: clearNewBugEvent,
});

sample({
  clock: createBugFx.done,
  target: clearNewBugEvent,
});

export const $combinedBugsStore = combine(
  $bugsData,
  $newBugStore,
  (bugsData, localBug) => {
    const { bugs, reportBugIds } = bugsData;

    const allBugIds = Object.values(reportBugIds).flat();

    const bugsFromStore = allBugIds.map((id: number) => {
      const bug = bugs[id];
      return {
        ...bug,
        // Используем сохраненный clientId, если он есть, иначе используем id
        clientId: bug.clientId || bug.id,
      };
    });

    if (localBug) {
      const localBugEntity = {
        ...localBug,
        id: localBug.clientId,
        status: BugStatuses.OPEN,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        creatorUserId: "",
        attachments: null,
        comments: null,
      };
      return bugsFromStore.concat([localBugEntity]);
    }

    return bugsFromStore;
  }
);
