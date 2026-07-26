import { usersPathWithContext } from "../instances";
import type { UserResponse } from "../contracts";

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
    ? usersPathWithContext("/users/avatar/content")
    : usersPathWithContext(`/users/${userId}/avatar/content`);

  return withCacheKey(avatarPath, imageUrl);
};

export const mapUserResponse = (user: UserResponse): UserResponse => {
  return {
    id: user.id,
    name: user.name,
    imageUrl: resolveAvatarUrl(user.id, user.imageUrl),
  };
};
