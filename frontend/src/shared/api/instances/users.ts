import { createApiInstance } from "./base";
import { getAppContext } from "./app";

/**
 * Users API
 * Путь: /api/users/v1/...
 * Бэкенд: users-api
 * Используется для: workspaces, teams, users, auth
 */
export const usersApi = createApiInstance();

// Хелпер для построения путей без контекста
export const usersPath = (path: string) =>
  `/api/users/v1${path.startsWith("/") ? path : `/${path}`}`;

/**
 * Хелпер для построения путей с контекстом workspace/team
 * Используется для эндпоинтов вида /workspaces/{wId}/teams/{tId}/...
 */
export const usersPathWithContext = (path: string) => {
  const { workspaceId, teamId } = getAppContext();
  const cleanPath = path.startsWith("/") ? path : `/${path}`;

  if (workspaceId && teamId) {
    return `/api/users/v1/workspaces/${workspaceId}/teams/${teamId}${cleanPath}`;
  }

  console.warn("Users context not set, request may fail:", path);
  return `/api/users/v1${cleanPath}`;
};
