import type * as usersApi from "@/shared/api/users";

/**
 * Формы модуля `users` — выведены из операций (`shared/api/users`), то есть из
 * `specs/contracts/users/openapi.yaml` вместе с путём и методом. Рукописных
 * копий этих форм здесь больше нет: совпадение с yaml держал глаз, теперь
 * держит компилятор.
 *
 * Регистр уже camelCase: тело перекладывает интерсептор
 * (`shared/api/instances/base.ts`), и это учтено в типах операций (ADR-0009).
 */

/** Пользователь так, как его отдают профиль и списковая ручка. */
export type UserResponse = usersApi.UserResult;

/** Участники команды и предел её размера. */
export type TeamMembersResponse = usersApi.TeamMembersResult;

/** Членство пользователя в команде. */
export type TeamMemberResponse = TeamMembersResponse["members"][number];

/** Стартовый экран: пространства с командами и оба вида членства. */
export type WorkspacesContextResponse = usersApi.WorkspacesContextResult;

/**
 * Рабочее пространство в контексте стартового экрана: идентификатор строкой и со
 * своими командами. Ответ создания и переименования — другая схема
 * (`usersApi.WorkspaceResult`: идентификатор числом, команд нет), и это не одно
 * и то же; рукописный DTO их смешивал.
 */
export type WorkspaceResponse = WorkspacesContextResponse["workspaces"][number];

/** Команда пространства так, как её отдают списковые ручки. */
export type TeamResponse = NonNullable<WorkspaceResponse["teams"]>[number];

/** Членство пользователя в пространстве. */
export type WorkspaceMemberResponse = NonNullable<
  WorkspacesContextResponse["workspacesMember"]
>[number];

/** Страница подсказок по пользователям. */
export type AutocompleteUsersResponse = usersApi.AutocompleteUsersResult;

/** Изменяемые поля профиля. */
export type UpdateUserRequest = usersApi.UpdateUserBody;

/** Привязанный способ входа. */
export type ExternalLinkResponse = usersApi.ExternalLinksResult[number];

/** Какой аккаунт влить в текущий. */
export type MergeUsersRequest = usersApi.MergeUsersBody;

/** Тело создания рабочего пространства. */
export type CreateWorkspaceRequest = usersApi.CreateWorkspaceBody;

/** Токен неинтерактивного доступа в списке: метаданные без секрета. */
export type PersonalAccessTokenResponse =
  usersApi.PersonalAccessTokensResult[number];

/** Что нужно для выпуска токена. */
export type CreatePersonalAccessTokenRequest =
  usersApi.CreatePersonalAccessTokenBody;

/** Ответ на выпуск: единственное место, где значение токена открыто. */
export type CreatedPersonalAccessTokenResponse =
  usersApi.CreatePersonalAccessTokenResult;
