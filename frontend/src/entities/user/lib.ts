import type { UserStoreModel } from "./model";
import { autocompleteUsers } from "./api";

type UserAutocompleteOption = {
  id: string;
  display: string;
  imageUrl?: string;
};

export function isAdmin(user: UserStoreModel | null | undefined): boolean {
  return user?.workspaceRole === "admin";
}

export const mattermostIdMaxLength = 26;

export function isMattermostIdValid(value: string): boolean {
  const trimmed = value.trim();
  return trimmed.length === mattermostIdMaxLength;
}

export const autocompleteUsersForAutosuggest = async (
  searchString: string
): Promise<UserAutocompleteOption[]> => {
  const response = await autocompleteUsers(searchString);
  return (response.users ?? []).map((user) => ({
    id: user.id,
    display: user.name,
    imageUrl: user.imageUrl ?? undefined,
  }));
};
