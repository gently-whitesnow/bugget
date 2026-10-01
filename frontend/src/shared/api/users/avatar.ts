import { requestInContext, urlInContext } from "./client";

const AVATAR = "/v1/workspaces/{workspaceId}/teams/{teamId}/users/avatar";
const AVATAR_CONTENT =
  "/v1/workspaces/{workspaceId}/teams/{teamId}/users/avatar/content";
const USER_AVATAR_CONTENT =
  "/v1/workspaces/{workspaceId}/teams/{teamId}/users/{userId}/avatar/content";

// Имя multipart-поля выведено из схемы `AvatarUpload`: multipart не проходит
// конверсию регистра, и опечатка доехала бы до бекенда как есть.
export const uploadAvatar = (file: File) =>
  requestInContext(AVATAR, "post", { multipart: { file } });

export const deleteAvatar = () => requestInContext(AVATAR, "delete", {});

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

// Аватар запрашивает браузер по `src`, поэтому адрес нужен строкой из контракта.
// `imageUrl` — ключ файла, а не ссылка: внешние ссылки отдаются как есть, свои
// становятся адресом ручки, а ключ уезжает в `v=` как ключ кеша браузера.
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

export const mapUserResponse = <
  T extends { id: string; imageUrl: string | null },
>(
  user: T
): T => ({
  ...user,
  imageUrl: resolveAvatarUrl(user.id, user.imageUrl),
});
