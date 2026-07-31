// Импорт из инстансов, а не из индекса `@/shared/api`: индекс тянет за собой
// транспортные границы модулей, а одна из них (`shared/api/reports`) собирает
// браузерный адрес вложения этим же хелпером.
import { getAppContext } from "@/shared/api/instances";

/**
 * Строит полный API URL с учетом workspace/team контекста
 * Используется для формирования URL к статическим ресурсам (например, картинки)
 * Использует origin страницы чтобы запросы шли через тот же прокси что и HTTP
 */
export const buildFullApiUrl = (path: string): string => {
  const baseUrl = window.location.origin;
  const cleanPath = path.startsWith("/") ? path : `/${path}`;

  const { workspaceId, teamId } = getAppContext();
  if (workspaceId && teamId) {
    return `${baseUrl}/api/app/workspaces/${workspaceId}/teams/${teamId}${cleanPath}`;
  }

  // Fallback - если контекст не установлен
  return `${baseUrl}/api/app${cleanPath}`;
};

/**
 * Строит полный URL для навигации внутри приложения
 * Учитывает режим работы и контекст workspace/team
 */
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

  // Fallback
  return cleanPath;
};

/**
 * @deprecated Используй buildFullApiUrl или buildFullAppUrl
 */
export const buildFullUrl = (baseUrl: string, path: string): string => {
  const baseUrlClean = baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl;
  const cleanPath = path.startsWith("/") ? path : `/${path}`;
  return `${baseUrlClean}${cleanPath}`;
};
