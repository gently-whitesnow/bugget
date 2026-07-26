import { usersApi, usersPath } from "./instances";
import { createTeam, updateTeam, deleteTeam } from "@/shared/api";
import type {
  WorkspaceResponse,
  TeamResponse,
  WorkspacesContextResponse,
} from "./contracts";

/**
 * Self-hosted API методы для bootstrap
 * Все пути начинаются с /api/users/ согласно nginx конфигурации
 */

/**
 * Присоединиться к рабочей области (workspace)
 * POST /api/users/v1/workspaces/{workspaceId}/members/join
 */
export async function joinWorkspace(
  workspaceId: string | number
): Promise<void> {
  await usersApi.post(usersPath(`/workspaces/${workspaceId}/members/join`));
}

/**
 * Присоединиться к команде
 * POST /api/users/v1/workspaces/{workspaceId}/teams/{teamId}/members/join
 */
export async function joinTeam(
  workspaceId: string | number,
  teamId: string | number
): Promise<void> {
  await usersApi.post(
    usersPath(`/workspaces/${workspaceId}/teams/${teamId}/members/join`)
  );
}

/**
 * Получить список workspaces с командами
 * GET /api/users/v1/workspaces
 */
export async function fetchWorkspacesContext(): Promise<WorkspacesContextResponse> {
  const { data } = await usersApi.get<WorkspacesContextResponse>(
    usersPath("/workspaces")
  );
  return data;
}

/**
 * Создать команду (только для admin)
 * POST /api/users/v1/workspaces/{workspaceId}/teams
 */
export { createTeam, updateTeam, deleteTeam };

export type { WorkspaceResponse, TeamResponse, WorkspacesContextResponse };
