import type { UserResponse } from "@/shared/api";

export type AutocompleteUsersResponse = {
  users: UserResponse[];
  total: number;
};

export type CurrentUserResponse = {
  id: string;
  name: string;
  imageUrl?: string | null;
  workspaceRole?: string | null;
  mattermostUserId?: string | null;
};

export type UpdateCurrentUserRequest = {
  name: string;
};

export type ExternalLink = {
  provider: string;
  externalId: string;
  email: string | null;
  linkedAt: string;
};

export type MergeAccountsRequest = {
  sourceUserId: string;
};
