import { createEffect, sample } from "effector";
import { createWorkspace, renameWorkspace, deleteWorkspace } from "./api";
import type {
  CreateWorkspaceRequest,
  WorkspaceResponse,
} from "./api/contracts";
import {
  $workspaces,
  addWorkspace,
  removeWorkspace,
  renameWorkspaceEvent,
  addTeamToWorkspaceEvent,
  fetchWorkspacesFx,
  resetWorkspaces,
} from "@/shared/model";

/**
 * Созданное пространство в форме стартового экрана. Контракт описывает два
 * разных ответа: создание отдаёт `Workspace` (идентификатор числом, команд нет),
 * а стор держит `WorkspaceWithTeams` из контекста (идентификатор строкой, у
 * пространства есть команды). Рукописный DTO смешивал их в один тип, и разница
 * держалась на том, что потребители всюду пишут `String(id)` и `teams || []`.
 * Приведение стало явным и живёт в одном месте — на проводе не изменилось ничего.
 */
const toWorkspaceWithTeams = (
  workspace: Awaited<ReturnType<typeof createWorkspace>>
): WorkspaceResponse => ({
  ...workspace,
  id: String(workspace.id),
  teams: [],
});

export const createWorkspaceFx = createEffect(
  async (dto: CreateWorkspaceRequest) => {
    const workspace = await createWorkspace(dto);
    return toWorkspaceWithTeams(workspace);
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
