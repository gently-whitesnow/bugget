import { usersApi, usersPath } from "@/shared/api/instances";
import type {
  TeamCreateInviteRequest,
  TeamInviteResponse,
} from "@/shared/api/contracts";

export async function createTeamInvite(
  workspaceId: string | number,
  teamId: string | number
): Promise<TeamCreateInviteRequest> {
  const { data } = await usersApi.post<TeamCreateInviteRequest>(
    usersPath(`/workspaces/${workspaceId}/teams/${teamId}/invites`)
  );
  return data;
}

export async function regenerateTeamInvite(
  workspaceId: string | number,
  teamId: string | number,
  inviteId: string | number
): Promise<TeamCreateInviteRequest> {
  const { data } = await usersApi.put<TeamCreateInviteRequest>(
    usersPath(`/workspaces/${workspaceId}/teams/${teamId}/invites/${inviteId}`)
  );
  return data;
}

export async function getTeamInvite(
  workspaceId: string | number,
  teamId: string | number
): Promise<TeamInviteResponse | null> {
  try {
    const { data } = await usersApi.get<TeamInviteResponse>(
      usersPath(`/workspaces/${workspaceId}/teams/${teamId}/invites`)
    );
    return data;
  } catch (error: unknown) {
    if (error && typeof error === "object" && "response" in error) {
      const axiosError = error as { response?: { status?: number } };
      if (axiosError.response?.status === 204) {
        return null;
      }
    }
    throw error;
  }
}

export async function deleteTeamInvite(
  workspaceId: string | number,
  teamId: string | number,
  inviteId: string | number
): Promise<void> {
  await usersApi.delete(
    usersPath(`/workspaces/${workspaceId}/teams/${teamId}/invites/${inviteId}`)
  );
}
