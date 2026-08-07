import { usersApi } from "@/shared/api";
import type {
  CreatePersonalAccessTokenRequest,
  CreatedPersonalAccessToken,
  PersonalAccessToken,
} from "./contracts";

/**
 * Токены неинтерактивного доступа текущего пользователя. Транспорт — операции
 * контракта (`shared/api/users`); здесь только имена, под которыми их зовут
 * настройки.
 */

export const fetchPersonalAccessTokens = (): Promise<PersonalAccessToken[]> =>
  usersApi.listPersonalAccessTokens();

export const createPersonalAccessToken = (
  request: CreatePersonalAccessTokenRequest
): Promise<CreatedPersonalAccessToken> =>
  usersApi.createPersonalAccessToken(request);

export const revokePersonalAccessToken = async (
  tokenId: string
): Promise<void> => {
  await usersApi.revokePersonalAccessToken(tokenId);
};
