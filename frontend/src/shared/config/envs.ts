export const basePath = window.env?.BASE_PATH || import.meta.env.VITE_BASE_PATH;
// URL для ссылки на домен (по умолчанию "/")
export const domainUrl = import.meta.env.VITE_DOMAIN_URL || "/";

// Тип авторизации для self-hosted: 'fake' | 'oidc'
export const authType =
  window.env?.AUTH_TYPE || import.meta.env.VITE_AUTH_TYPE || "fake";

// Флаги обязательных настроек пользователя
export const userNameRequired =
  (window.env?.USER_NAME_REQUIRED ||
    import.meta.env.VITE_USER_NAME_REQUIRED) === "true";
export const mattermostUserIdRequired =
  (window.env?.MATTERMOST_USER_ID_REQUIRED ||
    import.meta.env.VITE_MATTERMOST_USER_ID_REQUIRED) === "true";

// URL для DM с ботом Mattermost (для ручной привязки)
export const mattermostBotDmUrl =
  window.env?.MATTERMOST_BOT_DM_URL ||
  import.meta.env.VITE_MATTERMOST_BOT_DM_URL ||
  "";
