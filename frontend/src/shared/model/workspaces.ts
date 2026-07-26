import { createEffect, createEvent, createStore } from "effector";
import { selfHostedApi } from "@/shared/api";
import type {
  TeamResponse,
  WorkspaceResponse,
  TeamMemberResponse,
  WorkspaceMemberResponse,
  WorkspacesContextResponse,
} from "@/shared/api";

// Эффект загрузки workspaces
export const fetchWorkspacesFx = createEffect<void, WorkspacesContextResponse>(
  async () => {
    return await selfHostedApi.fetchWorkspacesContext();
  }
);

// Events for local updates
export const addWorkspace = createEvent<WorkspaceResponse>();
export const removeWorkspace = createEvent<string | number>();
export const renameWorkspaceEvent = createEvent<{
  workspaceId: string | number;
  name: string;
}>();
export const addTeamToWorkspaceEvent = createEvent<{
  workspaceId: string | number;
  team: TeamResponse;
}>();
export const resetWorkspaces = createEvent();

// Стор workspaces
export const $workspaces = createStore<WorkspaceResponse[]>([])
  .on(fetchWorkspacesFx.doneData, (_, context) => context.workspaces)
  .on(addWorkspace, (workspaces, newWorkspace) => [...workspaces, newWorkspace])
  .on(removeWorkspace, (workspaces, deletedId) =>
    workspaces.filter((workspace) => String(workspace.id) !== String(deletedId))
  )
  .on(renameWorkspaceEvent, (workspaces, { workspaceId, name }) =>
    workspaces.map((workspace) =>
      String(workspace.id) === String(workspaceId)
        ? { ...workspace, name }
        : workspace
    )
  )
  .on(addTeamToWorkspaceEvent, (workspaces, { workspaceId, team }) => {
    return workspaces.map((workspace) => {
      if (String(workspace.id) === String(workspaceId)) {
        return {
          ...workspace,
          teams: [...(workspace.teams || []), team],
        };
      }
      return workspace;
    });
  })
  .reset(resetWorkspaces);

export const $teamsMember = createStore<TeamMemberResponse[]>([])
  .on(fetchWorkspacesFx.doneData, (_, context) => context.teamsMember ?? [])
  .reset(resetWorkspaces);

export const $workspacesMember = createStore<WorkspaceMemberResponse[]>([])
  .on(
    fetchWorkspacesFx.doneData,
    (_, context) => context.workspacesMember ?? []
  )
  .reset(resetWorkspaces);
