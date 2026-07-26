import { usersApi, usersPath } from "@/shared/api/instances";
import type {
  WorkspaceResponse,
  CreateWorkspaceRequest,
} from "@/shared/api/contracts";

export async function createWorkspace(
  request: CreateWorkspaceRequest
): Promise<WorkspaceResponse> {
  const { data } = await usersApi.post<WorkspaceResponse>(
    usersPath("/workspaces"),
    request
  );
  return data;
}

export async function renameWorkspace(
  workspaceId: string | number,
  name: string
): Promise<WorkspaceResponse> {
  const { data } = await usersApi.put<WorkspaceResponse>(
    usersPath(`/workspaces/${workspaceId}`),
    { name }
  );
  return data;
}

export async function deleteWorkspace(workspaceId: number): Promise<void> {
  await usersApi.delete(usersPath(`/workspaces/${workspaceId}`));
}
