import { requestInContext, urlInContext } from "./client";

/* ── Свой аватар ───────────────────────────────────────────────────────────── */

const AVATAR = "/v1/workspaces/{workspaceId}/teams/{teamId}/users/avatar";
const AVATAR_CONTENT =
  "/v1/workspaces/{workspaceId}/teams/{teamId}/users/avatar/content";
const USER_AVATAR_CONTENT =
  "/v1/workspaces/{workspaceId}/teams/{teamId}/users/{userId}/avatar/content";

/**
 * Загрузка аватара — единственный multipart модуля. Имя поля выведено из схемы
 * `AvatarUpload`, а не написано строкой рядом: тело multipart конверсию регистра
 * не проходит, и опечатка в имени доехала бы до бекенда как есть.
 */
export const uploadAvatar = (file: File) =>
  requestInContext(AVATAR, "post", { multipart: { file } });

export const deleteAvatar = () => requestInContext(AVATAR, "delete", {});

/* ── Ссылка на содержимое аватара ──────────────────────────────────────────── */

type ResolveAvatarUrlOptions = {
  useCurrentUserEndpoint?: boolean;
};

export const isExternalUrl = (value: string): boolean => {
  return (
    value.startsWith("http://") ||
    value.startsWith("https://") ||
    value.startsWith("/")
  );
};

export const withCacheKey = (url: string, cacheKey: string): string => {
  const separator = url.includes("?") ? "&" : "?";
  return `${url}${separator}v=${encodeURIComponent(cacheKey)}`;
};

/**
 * Содержимое аватара запрашивает браузер по `src` картинки, а не axios, поэтому
 * здесь нужен адрес строкой. Шаблон и он берёт из контракта — иначе адрес
 * картинки разошёлся бы с адресом ручки молча.
 *
 * `imageUrl` в ответе — ключ файла в хранилище, а не ссылка: внешние ссылки
 * (пришедшие от провайдера входа) отдаются как есть, свои превращаются в адрес
 * ручки, а сам ключ уезжает в `v=` и служит ключом кеша браузера.
 */
export const resolveAvatarUrl = (
  userId: string,
  imageUrl: string | null | undefined,
  options?: ResolveAvatarUrlOptions
): string | null => {
  if (!imageUrl) {
    return null;
  }

  if (isExternalUrl(imageUrl)) {
    return imageUrl;
  }

  const avatarPath = options?.useCurrentUserEndpoint
    ? urlInContext(AVATAR_CONTENT)
    : urlInContext(USER_AVATAR_CONTENT, { userId });

  return withCacheKey(avatarPath, imageUrl);
};

/** Пользователь из ответа контракта с разобранной ссылкой на аватар. */
export const mapUserResponse = <
  T extends { id: string; imageUrl: string | null },
>(
  user: T
): T => ({
  ...user,
  imageUrl: resolveAvatarUrl(user.id, user.imageUrl),
});
