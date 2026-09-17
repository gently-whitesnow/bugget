import { createApiInstance } from "./base";

/** Users API (users-api): workspaces, teams, users, auth. */
export const usersApi = createApiInstance();

/**
 * Пути контракта начинаются с `/v1`, а nginx отдаёт users-api по `/api/users` —
 * этот шов живёт здесь и больше нигде.
 */
export const USERS_API_PREFIX = "/api/users";

// Уже полный путь и абсолютный URL пропускаются как есть.
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
