import { usersApi } from "@/shared/api";
import type {
  AutocompleteUsersResponse,
  CurrentUserResponse,
  ExternalLink,
  UpdateCurrentUserRequest,
} from "./contracts";

/**
 * Ручки текущего пользователя. Транспорт — операции контракта
 * (`shared/api/users`); здесь остаётся только то, что к контракту отношения не
 * имеет: разбор ключа аватара в адрес картинки.
 */

const mapCurrentUserResponse = (
  user: CurrentUserResponse
): CurrentUserResponse => ({
  ...user,
  imageUrl: usersApi.resolveAvatarUrl(user.id, user.imageUrl, {
    useCurrentUserEndpoint: true,
  }),
});

export const autocompleteUsers = async (
  searchString: string,
  skip: number = 0,
  take: number = 10
): Promise<AutocompleteUsersResponse> => {
  const data = await usersApi.autocompleteUsers({ searchString, skip, take });

  return {
    ...data,
    users: data.users.map(usersApi.mapUserResponse),
  };
};

export const fetchUsers = usersApi.fetchUsers;

/** Текущий пользователь: рабочее пространство и команда приходят аргументами. */
export const fetchCurrentUser = async (
  workspaceId?: string | number,
  teamId?: string | number
): Promise<CurrentUserResponse> => {
  const user = await usersApi.getUser(workspaceId, teamId);
  return mapCurrentUserResponse(user);
};

export const getUsersByIds = async (
  workspaceId: string | number,
  teamId: string | number,
  userIds: string[]
): Promise<CurrentUserResponse[]> => {
  const users = await usersApi.listUsers(workspaceId, teamId, userIds);
  return users.map(usersApi.mapUserResponse);
};

export const updateCurrentUser = async (
  request: UpdateCurrentUserRequest
): Promise<void> => {
  await usersApi.updateUserInContext(request);
};

export const uploadCurrentUserAvatar = async (file: File): Promise<void> => {
  await usersApi.uploadAvatar(file);
};

export const deleteCurrentUserAvatar = async (): Promise<void> => {
  await usersApi.deleteAvatar();
};

export const linkMattermost = async (
  mattermostUserId: string
): Promise<void> => {
  await usersApi.linkMattermost({ mattermostUserId });
};

export const disconnectMattermost = async (): Promise<void> => {
  await usersApi.unlinkMattermost();
};

export const fetchExternalLinks = (): Promise<ExternalLink[]> =>
  usersApi.listExternalLinks();

export const unlinkProvider = async (provider: string): Promise<void> => {
  await usersApi.unlinkProvider(provider);
};

export const mergeAccounts = async (sourceUserId: string): Promise<void> => {
  await usersApi.mergeUsers({ sourceUserId });
};
