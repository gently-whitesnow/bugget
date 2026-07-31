import { createApiInstance } from "./base";

/**
 * Authorization API
 * Путь: /api/authorization/v1/...
 * Бэкенд: authorization-api
 * Используется для: logout (без auth_request в nginx)
 */
export const authorizationApi = createApiInstance();

/**
 * Префикс модуля в адресе. Пути в `specs/contracts/authorization/openapi.yaml`
 * начинаются с `/v1`, а nginx отдаёт authorization-api по `/api/authorization` —
 * этот шов живёт здесь и больше нигде. Раньше его дописывал call-site хелпером
 * `authorizationPath`, то есть адрес собирался рядом с вызовом, а не из контракта.
 */
export const AUTHORIZATION_API_PREFIX = "/api/authorization";

// Тот же приём, что у usersApi: адрес операции получает префикс модуля ровно в
// одном месте. Уже полный путь и абсолютный URL пропускаются как есть.
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
