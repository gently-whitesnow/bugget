declare global {
  interface Window {
    env?: {
      BASE_PATH?: string;
      USERS_API_URL?: string;
      AUTH_TYPE?: string;
      USER_NAME_REQUIRED?: string;
      MATTERMOST_USER_ID_REQUIRED?: string;
      MATTERMOST_BOT_DM_URL?: string;
    };
  }
}

export {};
