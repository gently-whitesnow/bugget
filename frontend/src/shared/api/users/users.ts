import { request, requestInContext } from "./client";
import type { Body, Query, Result } from "./client";
import { mapUserResponse } from "./avatar";

const USER_IN_CONTEXT = "/v1/workspaces/{workspaceId}/teams/{teamId}/users";
const USERS_BATCH_LIST =
  "/v1/workspaces/{workspaceId}/teams/{teamId}/users/batch/list";

export type UserResult = Result<typeof USER_IN_CONTEXT, "get">;
export type UpdateUserBody = Body<typeof USER_IN_CONTEXT, "put">;
export type ListUsersResult = Result<typeof USERS_BATCH_LIST, "post">;

// Сегменты ручка игнорирует, но адрес обязан остаться прежним: значения
// подставляются как есть, включая `undefined` у не готового контекста.
export const getUser = (
  workspaceId?: string | number,
  teamId?: string | number
) =>
  request(USER_IN_CONTEXT, "get", {
    path: { workspaceId: String(workspaceId), teamId: String(teamId) },
  });

export const updateUserInContext = (body: UpdateUserBody) =>
  requestInContext(USER_IN_CONTEXT, "put", { body });

export const listUsers = (
  workspaceId: string | number,
  teamId: string | number,
  userIds: string[]
) =>
  request(USERS_BATCH_LIST, "post", {
    path: { workspaceId: String(workspaceId), teamId: String(teamId) },
    body: userIds,
  });

export const listUsersInContext = (userIds: string[]) =>
  requestInContext(USERS_BATCH_LIST, "post", { body: userIds });

/** Пользователи с разобранной ссылкой на аватар — форма, которую ждут сторы. */
export const fetchUsers = async (
  userIds: string[]
): Promise<ListUsersResult> => {
  if (userIds.length === 0) return [];

  const users = await listUsersInContext(userIds);
  return users.map(mapUserResponse);
};

const USERS_AUTOCOMPLETE =
  "/v1/workspaces/{workspaceId}/teams/{teamId}/users/autocomplete";

export type AutocompleteUsersQuery = Query<typeof USERS_AUTOCOMPLETE, "get">;
export type AutocompleteUsersResult = Result<typeof USERS_AUTOCOMPLETE, "get">;

export const autocompleteUsers = (query: AutocompleteUsersQuery) =>
  requestInContext(USERS_AUTOCOMPLETE, "get", { query });

const EXTERNAL_LINKS =
  "/v1/workspaces/{workspaceId}/teams/{teamId}/users/external-links";
const EXTERNAL_LINK =
  "/v1/workspaces/{workspaceId}/teams/{teamId}/users/external-links/{provider}";
const MERGE_USERS = "/v1/workspaces/{workspaceId}/teams/{teamId}/users/merge";

export type ExternalLinksResult = Result<typeof EXTERNAL_LINKS, "get">;
export type MergeUsersBody = Body<typeof MERGE_USERS, "post">;

export const listExternalLinks = () =>
  requestInContext(EXTERNAL_LINKS, "get", {});

export const unlinkProvider = (provider: string) =>
  requestInContext(EXTERNAL_LINK, "delete", { path: { provider } });

export const mergeUsers = (body: MergeUsersBody) =>
  requestInContext(MERGE_USERS, "post", { body });

const MATTERMOST =
  "/v1/workspaces/{workspaceId}/teams/{teamId}/users/mattermost";

export type LinkMattermostBody = Body<typeof MATTERMOST, "put">;

export const linkMattermost = (body: LinkMattermostBody) =>
  requestInContext(MATTERMOST, "put", { body });

export const unlinkMattermost = () =>
  requestInContext(MATTERMOST, "delete", {});
