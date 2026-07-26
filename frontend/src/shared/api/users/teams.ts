import { getAppContext, usersApi, usersPath } from "@/shared/api/instances";
import type {
  TeamResponse,
  TeamsAutocompleteResponse,
} from "@/shared/api/contracts";

export async function createTeam(
  workspaceId: string | number,
  name: string
): Promise<TeamResponse> {
  const { data } = await usersApi.post<TeamResponse>(
    usersPath(`/workspaces/${workspaceId}/teams`),
    { name }
  );
  return data;
}

export async function updateTeam(
  workspaceId: string | number,
  teamId: string | number,
  name: string
): Promise<TeamResponse> {
  const { data } = await usersApi.put<TeamResponse>(
    usersPath(`/workspaces/${workspaceId}/teams/${teamId}`),
    { name }
  );
  return data;
}

export async function deleteTeam(
  workspaceId: string | number,
  teamId: string | number
): Promise<void> {
  await usersApi.delete(
    usersPath(`/workspaces/${workspaceId}/teams/${teamId}`)
  );
}

export const teamsAutocomplete = async (
  searchString: string,
  skip: number = 0,
  take: number = 10
): Promise<TeamsAutocompleteResponse> => {
  try {
    const { workspaceId } = getAppContext();
    if (!workspaceId) {
      console.warn("Workspace context is not set for teams autocomplete");
      return { teams: [], total: 0 };
    }

    const { data } = await usersApi.get<TeamsAutocompleteResponse>(
      usersPath(`/workspaces/${workspaceId}/teams/autocomplete`),
      { params: { searchString, skip, take } }
    );
    return data;
  } catch (error) {
    console.error(error);
    return { teams: [], total: 0 };
  }
};
