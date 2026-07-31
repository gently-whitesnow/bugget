/**
 * Формы текущего пользователя — те же, что у модуля `users` в
 * `shared/api/contracts`, то есть выведенные из
 * `specs/contracts/users/openapi.yaml`. Рукописных DTO здесь больше нет: этот
 * файл остался только затем, чтобы имена, под которыми формы знает страница
 * профиля, не разъехались с именами схем контракта.
 *
 * Текущий пользователь и пользователь из списковой ручки — одна схема `User`:
 * рукописная копия отличалась от неё только необязательностью полей.
 */
export type {
  AutocompleteUsersResponse,
  UserResponse as CurrentUserResponse,
  UpdateUserRequest as UpdateCurrentUserRequest,
  ExternalLinkResponse as ExternalLink,
  MergeUsersRequest as MergeAccountsRequest,
} from "@/shared/api";
