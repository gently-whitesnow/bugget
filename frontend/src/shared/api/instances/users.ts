import { createApiInstance } from "./base";

/**
 * Users API
 * Путь: /api/users/v1/...
 * Бэкенд: users-api
 * Используется для: workspaces, teams, users, auth
 */
export const usersApi = createApiInstance();

/**
 * Префикс модуля в адресе. Пути в `specs/contracts/users/openapi.yaml` начинаются
 * с `/v1`, а nginx отдаёт users-api по `/api/users` — этот шов живёт здесь и
 * больше нигде.
 */
export const USERS_API_PREFIX = "/api/users";

// Тот же приём, что у appApi: адрес операции получает префикс модуля ровно в
// одном месте, а не склеивается заново на каждом call-site. Уже полный путь
// (`/api/users/...`) и абсолютный URL пропускаются как есть.
usersApi.interceptors.request.use((config) => {
  if (!config.url) return config;
  if (
    config.url.startsWith("http") ||
    config.url.startsWith(`${USERS_API_PREFIX}/`)
  ) {
    return config;
  }

  const path = config.url.startsWith("/") ? config.url : `/${config.url}`;
  config.url = `${USERS_API_PREFIX}${path}`;

  return config;
});
