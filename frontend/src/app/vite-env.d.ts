/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_BASE_PATH: string;
  readonly VITE_USERS_API_URL: string;
  readonly VITE_DOMAIN_URL?: string;
  readonly VITE_AUTH_TYPE?: string;
  readonly VITE_USER_NAME_REQUIRED?: string;
  readonly VITE_MATTERMOST_USER_ID_REQUIRED?: string;
  readonly VITE_MATTERMOST_BOT_DM_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
