import { createEffect, sample } from "effector";
import { createWorkspace, renameWorkspace, deleteWorkspace } from "./api";
import type { CreateWorkspaceRequest } from "./api/contracts";
import {
  $workspaces,
  addWorkspace,
  removeWorkspace,
  renameWorkspaceEvent,
  addTeamToWorkspaceEvent,
  fetchWorkspacesFx,
  resetWorkspaces,
} from "@/shared/model";

export const createWorkspaceFx = createEffect(
  async (dto: CreateWorkspaceRequest) => {
    const workspace = await createWorkspace(dto);
    return workspace;
  }
);

export const renameWorkspaceFx = createEffect(
  async ({
    workspaceId,
    name,
  }: {
    workspaceId: string | number;
    name: string;
  }) => {
    await renameWorkspace(workspaceId, name);
    return { workspaceId, name };
  }
);

export const deleteWorkspaceFx = createEffect(async (workspaceId: number) => {
  await deleteWorkspace(workspaceId);
  return workspaceId;
});

sample({
  clock: createWorkspaceFx.doneData,
  target: addWorkspace,
});

sample({
  clock: renameWorkspaceFx.doneData,
  target: renameWorkspaceEvent,
});

sample({
  clock: deleteWorkspaceFx.doneData,
  target: removeWorkspace,
});

// re-export shared store and events for compatibility
export {
  $workspaces,
  fetchWorkspacesFx,
  resetWorkspaces,
  addTeamToWorkspaceEvent,
};
