import { createApiInstance } from "./base";

/**
 * Beta-bot bot-api
 * Публичный путь: /api/bot/v1/bot-api/... (nginx снимает /api/bot/ → beta-bot видит /v1/bot-api/...).
 * Бэкенд: beta-bot (SaaS only)
 *
 * Интерсептор добавляет префикс `/api/bot/v1/bot-api` к относительным путям.
 * Пример: `botApi.get("/workspaces/{w}/beta-test")` → `/api/bot/v1/bot-api/workspaces/{w}/beta-test`.
 */
export const botApi = createApiInstance();

botApi.interceptors.request.use((config) => {
  if (!config.url) return config;

  if (config.url.startsWith("http") || config.url.startsWith("/api/")) {
    return config;
  }

  const path = config.url.startsWith("/") ? config.url : `/${config.url}`;
  config.url = `/api/bot/v1/bot-api${path}`;
  return config;
});
