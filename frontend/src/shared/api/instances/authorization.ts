import { createApiInstance } from "./base";

/**
 * Authorization API
 * Путь: /api/authorization/v1/...
 * Бэкенд: authorization-api
 * Используется для: login, logout, auth проверки (без auth_request в nginx)
 */
export const authorizationApi = createApiInstance();

// Хелпер для построения путей
export const authorizationPath = (path: string) =>
  `/api/authorization/v1${path.startsWith("/") ? path : `/${path}`}`;
