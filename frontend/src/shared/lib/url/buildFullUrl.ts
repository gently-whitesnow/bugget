// Импорт из инстансов, а не из индекса `@/shared/api`: индекс тянет за собой
// транспортные границы модулей, а одна из них (`shared/api/reports`) собирает
// браузерный адрес вложения этим же хелпером.
import { getAppContext } from "@/shared/api/instances";

// Origin страницы: запросы браузера за статикой идут через тот же прокси.
export const buildFullApiUrl = (path: string): string => {
  const baseUrl = window.location.origin;
  const cleanPath = path.startsWith("/") ? path : `/${path}`;

  const { workspaceId, teamId } = getAppContext();
  if (workspaceId && teamId) {
    return `${baseUrl}/api/app/workspaces/${workspaceId}/teams/${teamId}${cleanPath}`;
  }

  return `${baseUrl}/api/app${cleanPath}`;
};

export const buildFullAppUrl = (
  path: string,
  overrides?: {
    workspaceId?: string | number | null;
    teamId?: string | number | null;
  }
): string => {
  const cleanPath = path.startsWith("/") ? path : `/${path}`;

  const { teamId: contextTeamId } = getAppContext();
  const teamId = overrides?.teamId ?? contextTeamId;

  if (teamId) {
    return `/teams/${teamId}${cleanPath}`;
  }

  return cleanPath;
};

/** @deprecated Используй buildFullApiUrl или buildFullAppUrl */
export const buildFullUrl = (baseUrl: string, path: string): string => {
  const baseUrlClean = baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl;
  const cleanPath = path.startsWith("/") ? path : `/${path}`;
  return `${baseUrlClean}${cleanPath}`;
};
