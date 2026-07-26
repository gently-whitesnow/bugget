import { createApiInstance } from "./base";
import { parseAppContextFromPath } from "./appContext";

/**
 * App API
 * Путь: /api/app/workspaces/{workspaceId}/teams/{teamId}/v2/...
 * Бэкенд: app-api
 */

// Глобальный контекст приложения
let currentWorkspaceId: string | number | null = null;
let currentTeamId: string | number | null = null;

export const setAppContext = (
  workspaceId: string | number | null,
  teamId: string | number | null
) => {
  currentWorkspaceId = workspaceId;
  currentTeamId = teamId;
};

export const getAppContext = () => ({
  workspaceId: currentWorkspaceId,
  teamId: currentTeamId,
});

const resolveAppContext = () => {
  if (currentWorkspaceId && currentTeamId) {
    return {
      workspaceId: currentWorkspaceId,
      teamId: currentTeamId,
    };
  }

  if (typeof window === "undefined") {
    return { workspaceId: null, teamId: null };
  }

  const derivedContext = parseAppContextFromPath(window.location.pathname);
  if (derivedContext.workspaceId && derivedContext.teamId) {
    setAppContext(derivedContext.workspaceId, derivedContext.teamId);
    return derivedContext;
  }

  return derivedContext;
};

export const appApi = createApiInstance();

// Интерцептор для добавления workspace/team в путь
appApi.interceptors.request.use((config) => {
  if (!config.url) return config;

  // Пропускаем абсолютные URL и уже сформированные пути
  if (config.url.startsWith("http") || config.url.startsWith("/api/")) {
    return config;
  }

  // Формируем полный путь
  const path = config.url.startsWith("/") ? config.url : `/${config.url}`;

  const { workspaceId, teamId } = resolveAppContext();

  if (!workspaceId || !teamId) {
    throw new Error(`App context not set for request: ${path}`);
  }

  // /api/app/workspaces/{wId}/teams/{tId}/v2/...
  config.url = `/api/app/workspaces/${workspaceId}/teams/${teamId}${path}`;

  return config;
});

/**
 * Хелпер для WebSocket URL (нужен полный путь сразу)
 */
export const getAppWebSocketUrl = (
  path: string,
  version: "v1" | "v2" = "v1"
) => {
  const cleanPath = path.startsWith("/") ? path : `/${path}`;
  const versionedPath = `/${version}${cleanPath}`;

  const { workspaceId, teamId } = resolveAppContext();

  if (workspaceId && teamId) {
    return `/api/app/workspaces/${workspaceId}/teams/${teamId}${versionedPath}`;
  }

  throw new Error("App context not set for WebSocket connection");
};
