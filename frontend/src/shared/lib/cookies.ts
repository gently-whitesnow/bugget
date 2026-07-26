/**
 * Устанавливает cookie
 */
export function setCookie(name: string, value: string, maxAge: number) {
  if (typeof document === "undefined") return;
  document.cookie = `${name}=${encodeURIComponent(value)}; path=/; max-age=${maxAge}; SameSite=Lax`;
}

/**
 * Получает значение cookie по имени
 */
export function getCookie(name: string): string | null {
  if (typeof document === "undefined") return null;
  const matches = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
  return matches ? decodeURIComponent(matches[1]) : null;
}

/**
 * Удаляет cookie
 */
export function deleteCookie(name: string) {
  if (typeof document === "undefined") return;
  document.cookie = `${name}=; path=/; max-age=0`;
}

/**
 * Константы для работы с invite токеном
 */
export const inviteTokenCookie = "invite_token";
export const inviteCookieMaxAge = 7 * 24 * 60 * 60; // 7 days in seconds
