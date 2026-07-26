const loginNextSessionKey = "login_next";
const oidcAutoAttemptedSessionKey = "oidc_auto_attempted";
const oidcAutoAttemptedSessionValue = "1";

export const readLoginNextFromSession = (): string | null => {
  return sessionStorage.getItem(loginNextSessionKey);
};

export const saveLoginNextToSession = (next: string): void => {
  sessionStorage.setItem(loginNextSessionKey, next);
};

export const clearLoginNextFromSession = (): void => {
  sessionStorage.removeItem(loginNextSessionKey);
};

export const hasOidcAutoAttempted = (): boolean => {
  return sessionStorage.getItem(oidcAutoAttemptedSessionKey) !== null;
};

export const markOidcAutoAttempted = (): void => {
  sessionStorage.setItem(
    oidcAutoAttemptedSessionKey,
    oidcAutoAttemptedSessionValue
  );
};
