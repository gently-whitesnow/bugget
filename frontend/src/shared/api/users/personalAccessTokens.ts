import { requestInContext } from "./client";
import type { Body, Result } from "./client";

/* ── Personal access tokens ────────────────────────────────────────────────── */

const PERSONAL_ACCESS_TOKENS =
  "/v1/workspaces/{workspaceId}/teams/{teamId}/users/personal-access-tokens";
const PERSONAL_ACCESS_TOKEN =
  "/v1/workspaces/{workspaceId}/teams/{teamId}/users/personal-access-tokens/{tokenId}";

export type PersonalAccessTokensResult = Result<
  typeof PERSONAL_ACCESS_TOKENS,
  "get"
>;

export type CreatePersonalAccessTokenBody = Body<
  typeof PERSONAL_ACCESS_TOKENS,
  "post"
>;

export type CreatePersonalAccessTokenResult = Result<
  typeof PERSONAL_ACCESS_TOKENS,
  "post"
>;

/**
 * Токены владельца по всем его командам, а не только по команде из адреса:
 * область действия каждого видна в самой строке. Просроченные остаются в
 * ответе, отозванные — нет.
 */
export const listPersonalAccessTokens = () =>
  requestInContext(PERSONAL_ACCESS_TOKENS, "get", {});

export const createPersonalAccessToken = (
  body: CreatePersonalAccessTokenBody
) => requestInContext(PERSONAL_ACCESS_TOKENS, "post", { body });

export const revokePersonalAccessToken = (tokenId: string) =>
  requestInContext(PERSONAL_ACCESS_TOKEN, "delete", { path: { tokenId } });
