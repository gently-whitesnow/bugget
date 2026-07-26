import {
  usersApi,
  usersPath,
  usersPathWithContext,
  fetchUsers as fetchUsersShared,
  mapUserResponse,
  resolveAvatarUrl,
} from "@/shared/api";
import type {
  AutocompleteUsersResponse,
  CurrentUserResponse,
  ExternalLink,
  UpdateCurrentUserRequest,
} from "./contracts";

const mapCurrentUserResponse = (
  user: CurrentUserResponse
): CurrentUserResponse => {
  return {
    ...user,
    imageUrl: resolveAvatarUrl(user.id, user.imageUrl, {
      useCurrentUserEndpoint: true,
    }),
  };
};

export const autocompleteUsers = async (
  searchString: string,
  skip: number = 0,
  take: number = 10
): Promise<AutocompleteUsersResponse> => {
  const { data } = await usersApi.get<AutocompleteUsersResponse>(
    usersPathWithContext("/users/autocomplete"),
    { params: { searchString, skip, take } }
  );
  return {
    ...data,
    users: data.users.map(mapUserResponse),
  };
};

export const fetchUsers = fetchUsersShared;

/**
 * Получить текущего пользователя
 * GET /api/users/v1/workspaces/{workspaceId}/teams/{teamId}/users
 */
export const fetchCurrentUser = async (
  workspaceId?: string | number,
  teamId?: string | number
): Promise<CurrentUserResponse> => {
  const { data } = await usersApi.get<CurrentUserResponse>(
    usersPath(`/workspaces/${workspaceId}/teams/${teamId}/users`)
  );
  return mapCurrentUserResponse(data);
};

export const getUsersByIds = async (
  workspaceId: string | number,
  teamId: string | number,
  userIds: (string | number)[]
): Promise<CurrentUserResponse[]> => {
  const { data } = await usersApi.post<CurrentUserResponse[]>(
    usersPath(`/workspaces/${workspaceId}/teams/${teamId}/users/batch/list`),
    userIds
  );
  return data.map((user) => ({
    ...user,
    imageUrl: resolveAvatarUrl(String(user.id), user.imageUrl),
  }));
};

/**
 * Обновить текущего пользователя (имя)
 * PUT /api/users/v1/workspaces/{workspaceId}/teams/{teamId}/users
 */
export const updateCurrentUser = async (
  request: UpdateCurrentUserRequest
): Promise<void> => {
  await usersApi.put(usersPathWithContext("/users"), request);
};

/**
 * Загрузить аватар текущего пользователя
 * POST /api/users/v1/workspaces/{workspaceId}/teams/{teamId}/users/avatar
 */
export const uploadCurrentUserAvatar = async (file: File): Promise<void> => {
  const formData = new FormData();
  formData.append("file", file);

  await usersApi.post(usersPathWithContext("/users/avatar"), formData, {
    headers: {
      "Content-Type": "multipart/form-data",
    },
  });
};

/**
 * Удалить аватар текущего пользователя
 * DELETE /api/users/v1/workspaces/{workspaceId}/teams/{teamId}/users/avatar
 */
export const deleteCurrentUserAvatar = async (): Promise<void> => {
  await usersApi.delete(usersPathWithContext("/users/avatar"));
};

/**
 * Привязать Mattermost аккаунт вручную
 * PUT /api/users/v1/workspaces/{workspaceId}/teams/{teamId}/users/mattermost
 */
export const linkMattermost = async (
  mattermostUserId: string
): Promise<void> => {
  await usersApi.put(usersPathWithContext("/users/mattermost"), {
    mattermostUserId,
  });
};

/**
 * Отвязать Mattermost аккаунт
 * DELETE /api/users/v1/workspaces/{workspaceId}/teams/{teamId}/users/mattermost
 */
export const disconnectMattermost = async (): Promise<void> => {
  await usersApi.delete(usersPathWithContext("/users/mattermost"));
};

/**
 * Получить список привязанных провайдеров
 * GET /api/users/v1/workspaces/{workspaceId}/teams/{teamId}/users/external-links
 */
export const fetchExternalLinks = async (): Promise<ExternalLink[]> => {
  const { data } = await usersApi.get<ExternalLink[]>(
    usersPathWithContext("/users/external-links")
  );
  return data;
};

/**
 * Отвязать провайдера
 * DELETE /api/users/v1/workspaces/{workspaceId}/teams/{teamId}/users/external-links/{provider}
 */
export const unlinkProvider = async (provider: string): Promise<void> => {
  await usersApi.delete(
    usersPathWithContext(`/users/external-links/${provider}`)
  );
};

/**
 * Мёрж аккаунтов: перенести данные sourceUser → текущий пользователь
 * POST /api/users/v1/workspaces/{workspaceId}/teams/{teamId}/users/merge
 */
export const mergeAccounts = async (sourceUserId: string): Promise<void> => {
  await usersApi.post(usersPathWithContext("/users/merge"), { sourceUserId });
};
