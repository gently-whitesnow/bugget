/**
 * Имена, под которыми страница профиля знает формы контракта `users`
 * (`specs/contracts/users/openapi.yaml`); рукописных DTO здесь нет.
 */
export type {
  AutocompleteUsersResponse,
  UserResponse as CurrentUserResponse,
  UpdateUserRequest as UpdateCurrentUserRequest,
  ExternalLinkResponse as ExternalLink,
  MergeUsersRequest as MergeAccountsRequest,
  PersonalAccessTokenResponse as PersonalAccessToken,
  CreatePersonalAccessTokenRequest,
  CreatedPersonalAccessTokenResponse as CreatedPersonalAccessToken,
} from "@/shared/api";
