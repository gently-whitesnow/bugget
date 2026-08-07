import type { PersonalAccessTokenResponse } from "@/shared/api";

/**
 * Токен выпущен не на текущую команду. Список приходит по всем командам
 * владельца, поэтому без такой пометки строки неразличимы: область действия
 * есть только в полях, а не в названии.
 */
export const isTokenOutOfCurrentTeam = (
  token: Pick<PersonalAccessTokenResponse, "teamId">,
  currentTeamId: string | number | null | undefined
): boolean => {
  if (
    currentTeamId === null ||
    currentTeamId === undefined ||
    currentTeamId === ""
  ) {
    return false;
  }

  return String(token.teamId) !== String(currentTeamId);
};

/**
 * Срок токена истёк. Просроченные остаются в списке — пользователь должен
 * видеть, что именно перестало работать, а не обнаруживать пропажу строки.
 */
export const isTokenExpired = (
  token: Pick<PersonalAccessTokenResponse, "expiresAt">,
  now: number
): boolean => {
  if (token.expiresAt === null) return false;

  const expiresAt = Date.parse(token.expiresAt);
  return !Number.isNaN(expiresAt) && expiresAt <= now;
};

export const formatTokenDate = (iso: string): string =>
  new Date(iso).toLocaleDateString("ru-RU", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
