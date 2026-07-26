import { usersApi, usersPath } from "@/shared/api/instances";
import type { TeamMembersResponse } from "@/shared/api/contracts";

export async function listTeamMembers(
  workspaceId: string | number,
  teamId: string | number
): Promise<TeamMembersResponse> {
  const { data } = await usersApi.get<TeamMembersResponse>(
    usersPath(`/workspaces/${workspaceId}/teams/${teamId}/members`)
  );
  return data;
}

export async function deleteTeamMember(
  workspaceId: string | number,
  teamId: string | number,
  userId: string | number
): Promise<void> {
  await usersApi.delete(
    usersPath(`/workspaces/${workspaceId}/teams/${teamId}/members/${userId}`)
  );
}

export async function leaveTeam(
  workspaceId: string | number,
  teamId: string | number
): Promise<void> {
  await usersApi.delete(
    usersPath(`/workspaces/${workspaceId}/teams/${teamId}/members`)
  );
}
