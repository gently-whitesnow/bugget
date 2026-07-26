export const getAuthEntryPath = (): string => "/login";

export const buildAuthRedirectUrl = (next: string): string =>
  `${getAuthEntryPath()}?next=${encodeURIComponent(next)}`;

export const getPostLogoutRedirectUrl = (): string => buildAuthRedirectUrl("/");
