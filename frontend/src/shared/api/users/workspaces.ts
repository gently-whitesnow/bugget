import { request } from "./client";
import type { Body, Result } from "./client";

const WORKSPACES = "/v1/workspaces";
const WORKSPACE = "/v1/workspaces/{workspaceId}";
const WORKSPACE_MEMBERS_JOIN = "/v1/workspaces/{workspaceId}/members/join";

export type CreateWorkspaceBody = Body<typeof WORKSPACES, "post">;
export type WorkspacesContextResult = Result<typeof WORKSPACES, "get">;

/** Пространства пользователя со всем контекстом стартового экрана. */
export const listWorkspacesContext = () => request(WORKSPACES, "get", {});

export const createWorkspace = (body: CreateWorkspaceBody) =>
  request(WORKSPACES, "post", { body });

export const renameWorkspace = (workspaceId: string | number, name: string) =>
  request(WORKSPACE, "put", {
    path: { workspaceId: Number(workspaceId) },
    body: { name },
  });

/**
 * Удаляется текущее пространство пользователя, а не то, что в адресе: сегмент
 * контракт объявляет игнорируемым и оставлен ради формы URL.
 */
export const deleteWorkspace = (workspaceId: string | number) =>
  request(WORKSPACE, "delete", {
    path: { workspaceId: String(workspaceId) },
  });

export const joinWorkspace = (workspaceId: string | number) =>
  request(WORKSPACE_MEMBERS_JOIN, "post", {
    path: { workspaceId: Number(workspaceId) },
  });
