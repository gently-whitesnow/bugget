import { createApiInstance } from "./base";

/** Authorization API (authorization-api): logout без auth_request в nginx. */
export const authorizationApi = createApiInstance();

/**
 * Пути контракта начинаются с `/v1`, а nginx отдаёт модуль по
 * `/api/authorization` — этот шов живёт здесь и больше нигде.
 */
export const AUTHORIZATION_API_PREFIX = "/api/authorization";

// Уже полный путь и абсолютный URL пропускаются как есть.
authorizationApi.interceptors.request.use((config) => {
  if (!config.url) return config;
  if (
    config.url.startsWith("http") ||
    config.url.startsWith(`${AUTHORIZATION_API_PREFIX}/`)
  ) {
    return config;
  }

  const path = config.url.startsWith("/") ? config.url : `/${config.url}`;
  config.url = `${AUTHORIZATION_API_PREFIX}${path}`;

  return config;
});
