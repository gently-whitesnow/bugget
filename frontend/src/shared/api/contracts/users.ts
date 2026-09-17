import type * as usersApi from "@/shared/api/users";

// Формы модуля `users` выведены из операций (`shared/api/users`); регистр уже
// camelCase — тело перекладывает интерсептор (ADR-0009).

export type UserResponse = usersApi.UserResult;

export type TeamMembersResponse = usersApi.TeamMembersResult;

export type TeamMemberResponse = TeamMembersResponse["members"][number];

export type WorkspacesContextResponse = usersApi.WorkspacesContextResult;

/**
 * Пространство стартового экрана: id строкой и с командами. Ответ создания и
 * переименования — другая схема (`usersApi.WorkspaceResult`), не смешивать.
 */
export type WorkspaceResponse = WorkspacesContextResponse["workspaces"][number];

export type TeamResponse = NonNullable<WorkspaceResponse["teams"]>[number];

export type WorkspaceMemberResponse = NonNullable<
  WorkspacesContextResponse["workspacesMember"]
>[number];

export type AutocompleteUsersResponse = usersApi.AutocompleteUsersResult;

export type UpdateUserRequest = usersApi.UpdateUserBody;

export type ExternalLinkResponse = usersApi.ExternalLinksResult[number];

export type MergeUsersRequest = usersApi.MergeUsersBody;

export type CreateWorkspaceRequest = usersApi.CreateWorkspaceBody;

/** Токен неинтерактивного доступа в списке: метаданные без секрета. */
export type PersonalAccessTokenResponse =
  usersApi.PersonalAccessTokensResult[number];

export type CreatePersonalAccessTokenRequest =
  usersApi.CreatePersonalAccessTokenBody;

/** Ответ на выпуск: единственное место, где значение токена открыто. */
export type CreatedPersonalAccessTokenResponse =
  usersApi.CreatePersonalAccessTokenResult;
